using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Movimiento "Ranged": mantiene una distancia óptima al player. Si está demasiado
    /// cerca, busca el tile alcanzable en este turno que maximice la distancia Manhattan
    /// hasta <see cref="IdealDistance"/>. Si ya está a distancia ideal o más, no se mueve.
    /// TECHNICAL.md §17.§B (kiting).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_KeepDistance : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan al player que el enemigo intenta mantener. Si la actual " +
                 "ya es >= ideal, no se mueve.")]
        public AIIntReader IdealDistance;

        public override string NodeName => "Keep Distance From Player";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            int idealDist = IdealDistance?.Read(context) ?? 4;
            int currentDist = selfCoord.Manhattan(playerCoord);
            if (currentDist >= idealDist) return AIResult.Failed;

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            var reachable = context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Failed;

            var best = selfCoord;
            int bestScore = currentDist;
            foreach (var candidate in reachable)
            {
                int dist = Mathf.Min(candidate.Manhattan(playerCoord), idealDist);
                if (dist <= bestScore) continue;
                bestScore = dist;
                best = candidate;
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
