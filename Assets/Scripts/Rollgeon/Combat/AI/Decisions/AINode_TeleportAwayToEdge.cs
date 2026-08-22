using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Reubica al que actúa en una casilla libre que esté a la vez lejos del jugador y pegada al
    /// borde de la sala.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va por <see cref="IPathedMovementService.Teleport"/> y no por un paso de caminata: como no
    /// "entra" a ninguna casilla, no dispara los <c>OnEnter</c> de las especiales del camino y el
    /// jefe no se come su propio fuego al escapar.
    /// </para>
    /// <para>
    /// Las dos condiciones se piden juntas y por separado. La distancia al jugador se clampea a
    /// <see cref="MinPlayerDistance"/> y la profundidad de borde a <see cref="EdgeBandDepth"/>, así
    /// que todas las casillas que cumplen empatan y el sorteo reparte entre la franja entera. Sin el
    /// clamp, "lo más lejos posible" y "lo más al borde posible" resuelven siempre a la misma
    /// esquina y la pelea se aprende de memoria.
    /// </para>
    /// <para>
    /// Sin <see cref="MaxDistanceFromPlayer"/> el jefe siempre encuentra casilla más lejos de lo que
    /// el jugador cubre en un turno, y la pelea no se puede ganar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TeleportAwayToEdge : AIActionNode
    {
        [Tooltip("Distancia Manhattan al jugador que ya alcanza para contar como lejos. Todas las " +
                 "casillas que la llegan o la pasan empatan entre sí.")]
        [MinValue(1)]
        public int MinPlayerDistance = 8;

        [Tooltip("Techo de distancia al jugador, en pasos de grafo (no Manhattan). 0 = sin tope. " +
                 "Con tope, el jugador siempre llega a tocar al jefe en su turno siguiente.")]
        [MinValue(0)]
        public int MaxDistanceFromPlayer = 0;

        [Tooltip("Anillos desde el perímetro que cuentan como borde. 0 = sólo el perímetro, 1 = el " +
                 "perímetro y la fila de adentro.")]
        [MinValue(0)]
        public int EdgeBandDepth = 1;

        [Tooltip("Consume el presupuesto de movimiento del turno (Move y KeepDistance). Con esto en " +
                 "false, un paso de movimiento posterior en el mismo Sequence saca al jefe del borde " +
                 "en el mismo turno en que escapó.")]
        public bool ConsumeMoveAction = true;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del salto, sólo en los turnos que reubican de verdad. Vacío = sin animación.")]
        public string TeleportFeedbackId;

        public override string NodeName => "Teleport Away To Edge";

        /// <summary>
        /// Si el último <see cref="Tick"/> reubicó de verdad. El nodo devuelve
        /// <see cref="AIResult.Succeeded"/> también cuando respeta un movimiento ya gastado, así que
        /// sin esto la animación del salto correría en turnos donde el jefe no se movió.
        /// </summary>
        [NonSerialized] private bool _movedThisTick;

        public override AIResult Tick(AIContext context)
        {
            _movedThisTick = false;
            if (context == null) return AIResult.Failed;

            // El movimiento del turno ya se gastó (el teleport al centro del Pleno lo consume):
            // Succeeded y sin mover, para no arrancar al jefe del lugar donde lo plantó ese paso ni
            // abortar el Sequence que sigue.
            if (context.HasExecuted(AINode_Move.ActionKey) ||
                context.HasExecuted(AINode_KeepDistance.ActionKey))
            {
                return AIResult.Succeeded;
            }

            // Todos los Failed de acá abajo son callados: el nodo corre en cada turno del jefe, y un
            // aviso por turno tapa la consola de la pelea entera.
            var grid = context.Grid;
            if (grid == null || context.Movement == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty ||
                !grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
            {
                return AIResult.Failed;
            }

            var candidates = CollectCandidates(grid, context.Movement, selfCoord, playerCoord);
            if (candidates.Count == 0) return AIResult.Failed;

            var rng = context.Rng ?? new System.Random();
            var destination = candidates[rng.Next(candidates.Count)];

            if (!Relocate(context, destination)) return AIResult.Failed;

            _movedThisTick = true;
            ConsumeMove(context);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result != AIResult.Succeeded || !_movedThisTick || string.IsNullOrEmpty(TeleportFeedbackId))
            {
                onResult?.Invoke(result);
                yield break;
            }

            var beat = PlayTeleport(context);
            while (beat.MoveNext()) yield return beat.Current;

            onResult?.Invoke(result);
        }

        /// <summary>
        /// Casillas empatadas en el mejor puntaje dentro del techo: primero las que más se acercan a
        /// <see cref="MinPlayerDistance"/>, y entre ésas las que caen en la franja de borde.
        /// </summary>
        /// <remarks>
        /// La propia casilla queda afuera: si empatara con las demás, el sorteo podría "escapar" al
        /// mismo lugar y el turno se leería como un salto que no pasó.
        /// </remarks>
        private List<GridCoord> CollectCandidates(
            IGridManager grid, IMovementService movement, GridCoord selfCoord, GridCoord playerCoord)
        {
            var best = new List<GridCoord>();

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito".
            // Materializado porque se recorre dos veces (bounds + puntaje).
            var tiles = new List<GridCoord>(ThreatAreaShape.RoomTiles(grid));
            if (tiles.Count == 0) return best;

            // El techo se mide en pasos del grafo y no en Manhattan: con obstáculos de por medio, dos
            // casillas a la misma Manhattan pueden estar a distinta cantidad de pasos, y la de más
            // pasos cae fuera de lo que el jugador cruza en su turno.
            HashSet<GridCoord> withinReach = null;
            if (MaxDistanceFromPlayer > 0)
            {
                var reachable = movement.GetReachableTiles(playerCoord, MaxDistanceFromPlayer);
                if (reachable == null || reachable.Count == 0) return best;
                withinReach = new HashSet<GridCoord>(reachable);
            }

            // Los bounds salen de la sala entera y no de las libres: la forma de la sala no puede
            // depender de quién esté parado adentro.
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            int bestFar = int.MinValue;
            int bestEdge = int.MaxValue;
            foreach (var c in tiles)
            {
                if (c == selfCoord || grid.IsOccupied(c)) continue;
                // El techo filtra y el piso sólo empata puntajes, así que un techo por debajo del
                // piso no deja el sorteo vacío: deja el piso sin alcanzar.
                if (withinReach != null && !withinReach.Contains(c)) continue;

                int far = Math.Min(c.Manhattan(playerCoord), Math.Max(1, MinPlayerDistance));
                int edge = EdgeRank(c, minX, maxX, minY, maxY);

                if (far < bestFar) continue;
                if (far == bestFar && edge > bestEdge) continue;
                if (far > bestFar || edge < bestEdge) best.Clear();

                bestFar = far;
                bestEdge = edge;
                best.Add(c);
            }

            // Orden estable antes del sorteo: NavGraph enumera sus nodos sin orden garantizado, y con
            // el pool en orden de enumeración la misma seed daría casillas distintas entre corridas.
            best.Sort(CompareByRowThenColumn);
            return best;
        }

        /// <summary>
        /// Anillos que le faltan a la casilla para entrar en la franja de borde; 0 = ya está en ella.
        /// </summary>
        /// <remarks>
        /// La profundidad es la distancia ortogonal al lado más cercano del bounding box, que da el
        /// mismo número en Manhattan y en Chebyshev — el eje transversal no aporta.
        /// </remarks>
        private int EdgeRank(GridCoord c, int minX, int maxX, int minY, int maxY)
        {
            int depth = Math.Min(Math.Min(c.X - minX, maxX - c.X), Math.Min(c.Y - minY, maxY - c.Y));
            return Math.Max(0, depth - Math.Max(0, EdgeBandDepth));
        }

        private static int CompareByRowThenColumn(GridCoord a, GridCoord b) =>
            a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);

        /// <remarks>
        /// Degradado a <see cref="IMovementService.Move"/> cuando el servicio no expone la interfaz
        /// aditiva: los fakes de los tests EditMode implementan sólo <c>IMovementService</c>. Camina
        /// en vez de teleportar, pero termina en la misma casilla.
        /// </remarks>
        private static bool Relocate(AIContext context, GridCoord destination)
        {
            if (context.Movement is IPathedMovementService pathed)
                return pathed.Teleport(context.SelfGuid, destination);

            return context.Movement.Move(context.SelfGuid, destination);
        }

        /// <remarks>
        /// Las keys de <c>AINode_Move</c> y <c>AINode_KeepDistance</c> porque es el mismo presupuesto:
        /// escapar ES el movimiento del turno. Los dos, no uno: son budgets separados, así que marcar
        /// sólo el de Move deja al paso de kiteo devolviendo al jefe hacia el jugador en el mismo
        /// turno en que escapó.
        /// </remarks>
        private void ConsumeMove(AIContext context)
        {
            if (!ConsumeMoveAction) return;
            context.MarkExecuted(AINode_Move.ActionKey);
            context.MarkExecuted(AINode_KeepDistance.ActionKey);
        }

        /// <remarks>
        /// Request armado a mano porque el nodo no nace de un effect pass y no tiene
        /// <c>EffectContext</c> que pasarle.
        /// </remarks>
        private IEnumerator PlayTeleport(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = TeleportFeedbackId,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.SelfGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el turno le pasa
            // por encima.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif
    }
}
