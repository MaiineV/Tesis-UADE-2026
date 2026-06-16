using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Grid;
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
    /// Generaliza el viejo "Move Toward Player": con <see cref="TargetSelector"/> null el
    /// resolver usa <c>TargetSelector_AlwaysPlayer</c>, y con <see cref="DesiredRange"/> null
    /// se cae al legacy <see cref="StopAdjacent"/> (rango 1). Con <see cref="Retreat"/> activo
    /// subsume también el comportamiento de <c>AINode_KeepDistance</c>.
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

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;

            var targetGuid = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            if (targetGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(targetGuid, out var targetCoord))
                return AIResult.Failed;

            int desiredRange = DesiredRange?.Read(context) ?? (StopAdjacent ? 1 : 0);
            int currentDist = selfCoord.Manhattan(targetCoord);

            if (currentDist == desiredRange) return AIResult.Failed;        // ya en la banda
            if (currentDist < desiredRange && !Retreat) return AIResult.Failed; // muy cerca, kite off

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            var reachable = context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Failed;

            // Score único: minimizar |dist(target) - desiredRange|. Cubre acercarse,
            // frenar en la banda y alejar (kite) con la misma pasada. Strict '<' =>
            // determinista y, ante empate con quedarse quieto, no se mueve.
            var best = selfCoord;
            int bestErr = Mathf.Abs(currentDist - desiredRange);
            foreach (var candidate in reachable)
            {
                int err = Mathf.Abs(candidate.Manhattan(targetCoord) - desiredRange);
                if (err < bestErr)
                {
                    bestErr = err;
                    best = candidate;
                }
            }

            if (best == selfCoord) return AIResult.Failed;

            if (!context.Movement.Move(context.SelfGuid, best))
                return AIResult.Failed;

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
