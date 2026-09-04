using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Reubica al que actúa en una casilla libre a corta distancia del jugador. La cara opuesta de
    /// <see cref="AINode_TeleportAwayToEdge"/>: en vez de huir, se le viene encima.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va por <see cref="IPathedMovementService.Teleport"/> y no por un paso de caminata: como no
    /// "entra" a ninguna casilla, no dispara los <c>OnEnter</c> de las especiales del camino.
    /// </para>
    /// <para>
    /// La distancia es una <b>banda</b> y no un mínimo, y todas las casillas que caen adentro
    /// empatan. Con un solo número el salto resuelve siempre a la misma casilla relativa y el
    /// acercamiento se aprende de memoria; con la banda, el jugador sabe que se le viene encima pero
    /// no exactamente a dónde.
    /// </para>
    /// <para>
    /// Pegado al jugador (<see cref="MinDistance"/> en 1) es un regalo para un jefe de kit a
    /// distancia: le entrega un turno franco de golpes sin caminar. La banda por defecto lo deja
    /// cerca pero todavía a un paso de distancia.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TeleportNearTarget : AIActionNode
    {
        [Tooltip("Piso de la banda, en Manhattan al jugador. 1 = adyacente.")]
        [MinValue(1)]
        public int MinDistance = 2;

        [Tooltip("Techo de la banda, en Manhattan al jugador. Todas las casillas entre el piso y el " +
                 "techo empatan y el sorteo reparte entre ellas.")]
        [MinValue(1)]
        public int MaxDistance = 3;

        [Tooltip("Consume el presupuesto de movimiento del turno (Move y KeepDistance). Con esto en " +
                 "false, un paso de movimiento posterior en el mismo Sequence deshace el salto.")]
        public bool ConsumeMoveAction = true;

        [Tooltip("Saca del sorteo las casillas que hacen daño. Con toda la banda ardiendo salta " +
                 "igual — es preferencia, no requisito.")]
        public bool AvoidHarmfulTiles = true;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del salto, sólo en los turnos que reubican de verdad. Vacío = sin animación.")]
        public string TeleportFeedbackId;

        public override string NodeName => $"Teleport Near Target ({MinDistance}-{MaxDistance})";

        /// <summary>
        /// Si el último <see cref="Tick"/> reubicó de verdad. El nodo devuelve
        /// <see cref="AIResult.Succeeded"/> también cuando respeta un movimiento ya gastado, así que
        /// sin esto la animación del salto correría en turnos donde no se movió.
        /// </summary>
        [NonSerialized] private bool _movedThisTick;

        public override AIResult Tick(AIContext context)
        {
            _movedThisTick = false;
            if (context == null) return AIResult.Failed;

            // El movimiento del turno ya se gastó: Succeeded y sin mover, para no arrancar al jefe
            // del lugar donde lo plantó ese paso ni abortar el Sequence que sigue.
            if (context.HasExecuted(AINode_Move.ActionKey) ||
                context.HasExecuted(AINode_KeepDistance.ActionKey))
            {
                return AIResult.Succeeded;
            }

            // Todos los Failed de acá abajo son callados: el nodo puede correr en cada turno del
            // jefe, y un aviso por turno tapa la consola de la pelea entera.
            var grid = context.Grid;
            if (grid == null || context.Movement == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty ||
                !grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
            {
                return AIResult.Failed;
            }

            var candidates = CollectCandidates(grid, selfCoord, playerCoord, AvoidHarmfulTiles);
            if (candidates.Count == 0 && AvoidHarmfulTiles)
                candidates = CollectCandidates(grid, selfCoord, playerCoord, false);
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
        /// Casillas libres de la sala que caen dentro de la banda. La propia queda afuera: si
        /// empatara, el sorteo podría "acercarse" al mismo lugar y el turno se leería como un salto
        /// que no pasó.
        /// </summary>
        private List<GridCoord> CollectCandidates(
            IGridManager grid, GridCoord selfCoord, GridCoord playerCoord, bool skipHarmful)
        {
            var pool = new List<GridCoord>();

            // Footprint del self (Fase B): un candidato solo vale si el rectángulo entero
            // cabe ahí. El guid se recupera del ancla propia; para 1×1 nada cambia.
            grid.TryGetOccupant(selfCoord, out var selfGuid);
            var selfFp = grid.GetFootprint(selfGuid);

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito".
            foreach (var c in ThreatAreaShape.RoomTiles(grid))
            {
                if (c == selfCoord || c == playerCoord) continue;
                if (!grid.CanPlace(c, selfFp, ignore: selfGuid)) continue;
                if (TeleportCellFilter.IsStrandedCell(grid, c)) continue;
                if (skipHarmful && HarmfulTileQuery.IsHarmfulAt(c)) continue;

                int distance = c.Manhattan(playerCoord);
                if (distance < MinDistance || distance > Math.Max(MinDistance, MaxDistance)) continue;

                pool.Add(c);
            }

            // Orden estable antes del sorteo: NavGraph enumera sus nodos sin orden garantizado, y con
            // el pool en orden de enumeración la misma seed daría casillas distintas entre corridas.
            pool.Sort(CompareByRowThenColumn);
            return pool;
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
        /// acercarse ES el movimiento del turno. Los dos, no uno: son budgets separados, así que
        /// marcar sólo el de Move deja al paso de kiteo alejándolo en el mismo turno en que cerró.
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
