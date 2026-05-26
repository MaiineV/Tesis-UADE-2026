using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Mueve al enemigo un máximo de <see cref="MaxSteps"/> tiles hacia el player.
    /// TECHNICAL.md §7.5 + §17.§B.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Move : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [Tooltip("Si true, frena al estar adyacente al player (no pisa el tile del player).")]
        public bool StopAdjacent = true;

        public override string NodeName => "Move Toward Player";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            if (StopAdjacent && selfCoord.Manhattan(playerCoord) <= 1)
                return AIResult.Failed;

            var target = ChooseTargetTile(context, selfCoord, playerCoord);
            if (target == selfCoord) return AIResult.Failed;

            var path = context.Movement.FindPath(selfCoord, target);
            if (path == null || path.Count < 2) return AIResult.Failed;

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            int steps = Mathf.Min(maxSteps, path.Count - 1);
            var destination = path[steps];

            if (!context.Movement.Move(context.SelfGuid, destination))
                return AIResult.Failed;

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }

        private static GridCoord ChooseTargetTile(AIContext context, GridCoord selfCoord, GridCoord playerCoord)
        {
            GridCoord best = selfCoord;
            int bestDist = int.MaxValue;
            foreach (var adj in playerCoord.Neighbors4())
            {
                if (!context.Grid.IsWalkable(adj)) continue;
                if (context.Grid.IsOccupied(adj)) continue;
                int d = selfCoord.Manhattan(adj);
                if (d < bestDist) { bestDist = d; best = adj; }
            }
            return best;
        }
    }
}
