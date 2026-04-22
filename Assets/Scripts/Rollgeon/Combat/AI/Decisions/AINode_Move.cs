using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Mueve al enemigo un máximo de <see cref="MaxSteps"/> tiles hacia el player.
    /// TECHNICAL.md §7.5 + §17.§B.
    /// </summary>
    /// <remarks>
    /// Pide <see cref="Rollgeon.Movement.IMovementService.FindPath"/> al tile adyacente
    /// al player más cercano al Self. Avanza hasta <c>MaxSteps</c>. Si no hay ruta, o Self
    /// ya está adyacente (y <see cref="StopAdjacent"/>), retorna <see cref="AIResult.Failed"/>
    /// para que el árbol ramifique (ej. "si no puedo moverme, espero").
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Move : AIActionNode
    {
        [MinValue(1)]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public int MaxSteps = 3;

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

            int steps = Mathf.Min(MaxSteps, path.Count - 1);
            var destination = path[steps];

            return context.Movement.Move(context.SelfGuid, destination)
                ? AIResult.Succeeded
                : AIResult.Failed;
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
