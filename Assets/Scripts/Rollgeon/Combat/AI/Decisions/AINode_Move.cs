using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Mueve al enemigo hacia un target configurable manteniendo una distancia deseada
    /// (<see cref="DesiredRange"/>). Se acerca si está lejos y, si <see cref="Retreat"/>,
    /// retrocede (kite) si está demasiado cerca. TECHNICAL.md §7.5 + §17.§B.
    /// </summary>
    /// <remarks>
    /// Con <see cref="TargetSelector"/> null el resolver usa <c>TargetSelector_AlwaysPlayer</c>, y
    /// con <see cref="DesiredRange"/> null se cae a <see cref="StopAdjacent"/> (rango 1).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Move : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("A quién apuntar. Null = player (TargetSelector_AlwaysPlayer).")]
        public BaseEnemyTargetSelector TargetSelector;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan al target que el enemigo intenta mantener. " +
                 "Null = legacy: StopAdjacent ? 1 : 0.")]
        public AIIntReader DesiredRange;

        [Tooltip("Si true y está más cerca que DesiredRange, retrocede (kite). " +
                 "Si false, demasiado cerca = no se mueve.")]
        public bool Retreat;

        [Tooltip("DEPRECADO — usar DesiredRange. Solo fallback cuando DesiredRange es null: " +
                 "true => rango 1 (frena adyacente), false => rango 0.")]
        public bool StopAdjacent = true;

        public override string NodeName => "Move Toward Target";

        /// <summary>Key compartida por todos los AINode_Move del árbol: mover es UNA acción por turno.</summary>
        // Público: el AITreeValidator (editor asmdef) lo usa para vetar ActionNames reservados.
        public const string ActionKey = "__move";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            // Ya movió este turno → no-op transparente (Succeeded, no Failed — un Failed
            // abortaría el While padre) para que el loop siga drenando energía.
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;

            var targetGuid = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            if (targetGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(targetGuid, out var targetCoord))
                return AIResult.Failed;

            int desiredRange = DesiredRange?.Read(context) ?? (StopAdjacent ? 1 : 0);
            // Rect-a-rect (Fase B): la distancia se mide desde la celda más cercana de cada
            // footprint; para dos 1×1 es el Manhattan de siempre.
            var selfFp = context.Grid.GetFootprint(context.SelfGuid);
            var targetFp = context.Grid.GetFootprint(targetGuid);
            int currentDist = GridFootprint.ManhattanDistance(selfCoord, selfFp, targetCoord, targetFp);

            // BUG-061/PUL-014: "no hace falta moverse" y "no HAY forma de moverse" son casos
            // benignos, no errores — Succeeded (no-op) para que la Sequence siga y el
            // enemigo pueda atacar si ya está en rango. Antes devolvían Failed y un
            // AINode_Sequence sin Selector que lo absorbiera abortaba el turno ENTERO,
            // incluido el ataque (un enemigo en una isla de 1 celda del NavGraph quedaba
            // trabado para siempre). Ver AIWrapFallible/Isolate en los builders de bosses:
            // son Selector[Move, Wait] — con Wait siempre Succeeded, este cambio no altera
            // su resultado final, solo evita el salto a Wait.
            if (currentDist == desiredRange) return AIResult.Succeeded;        // ya en la banda
            if (currentDist < desiredRange && !Retreat) return AIResult.Succeeded; // muy cerca, kite off

            int maxSteps = MaxSteps?.Read(context) ?? 3;

            // Con planner en el contexto, la decisión es suya (conciencia de casillas
            // especiales; con sala limpia su resultado es idéntico al scoring de abajo).
            if (context.PathPlanner != null)
            {
                if (!AIPathMoveExecutor.TryPlanAndMove(context, targetCoord, maxSteps, desiredRange,
                        Pathing.MoveIntent.Approach))
                {
                    return AIResult.Succeeded; // NoMove del planner (isla, sin tile mejor): no-op
                }
            }
            else
            {
                // GetReachableAnchors respeta el footprint (un 2×2 no entra por un pasillo
                // de 1); los fakes de tests sin la interfaz aditiva degradan al BFS 1×1.
                var reachable = (context.Movement as IPathedMovementService)
                        ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                    ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
                if (reachable == null || reachable.Count == 0) return AIResult.Succeeded; // sin candidato BFS

                // Score único: minimizar |dist(target) - desiredRange|. Cubre acercarse,
                // frenar en la banda y alejar (kite) con la misma pasada. Strict '<' =>
                // determinista y, ante empate con quedarse quieto, no se mueve.
                var best = selfCoord;
                int bestErr = Mathf.Abs(currentDist - desiredRange);
                foreach (var candidate in reachable)
                {
                    int err = Mathf.Abs(GridFootprint.ManhattanDistance(candidate, selfFp, targetCoord, targetFp) - desiredRange);
                    if (err < bestErr)
                    {
                        bestErr = err;
                        best = candidate;
                    }
                }

                if (best == selfCoord) return AIResult.Succeeded; // ningún tile alcanzable mejora la banda

                if (!context.Movement.Move(context.SelfGuid, best))
                    return AIResult.Succeeded; // Move rechazó el destino (ocupado, etc.)
            }

            // Solo el movimiento efectivo consume la acción — los Failed de arriba
            // (ya en banda, sin tile mejor) dejan la acción disponible.
            context.MarkExecuted(ActionKey);

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }
    }
}
