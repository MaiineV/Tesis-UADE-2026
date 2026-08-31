using System;
using Patterns;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si el opponent del context está a ≤ <see cref="Range"/> del owner según
    /// <see cref="Metric"/>. Reemplaza a <c>AICond_PlayerInRange</c> generalizado al
    /// target ya resuelto por el contenedor (no asume que sea siempre el player).
    /// </summary>
    /// <remarks>
    /// Distinto de <see cref="PCEntityInRange"/> en intención: este es el equivalente
    /// directo del viejo AI-side check. Misma matemática, mismo fallback false si owner
    /// u opponent no están en grid.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTargetInRange : BasePreCondition
    {
        [MinValue(0)]
        public int Range = 1;

        public DistanceMetric Metric = DistanceMetric.Manhattan;

        public override string ConditionName => $"Target in {Metric} range ≤ {Range}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return false;
            if (context.OwnerGuid == Guid.Empty || context.OpponentGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;

            if (!grid.TryGetPosition(context.OwnerGuid, out var ownerCoord)) return false;
            if (!grid.TryGetPosition(context.OpponentGuid, out var opponentCoord)) return false;

            // Rect-a-rect (Fase B): la distancia se mide desde la celda más cercana de cada
            // footprint — un 2×2 melee pegado por una celda no-ancla está "adyacente".
            // Para dos 1×1 equivale a la matemática de siempre.
            var ownerFp = grid.GetFootprint(context.OwnerGuid);
            var opponentFp = grid.GetFootprint(context.OpponentGuid);
            int dist = Metric == DistanceMetric.Manhattan
                ? GridFootprint.ManhattanDistance(ownerCoord, ownerFp, opponentCoord, opponentFp)
                : GridFootprint.ChebyshevDistance(ownerCoord, ownerFp, opponentCoord, opponentFp);

            return dist <= Range;
        }
    }
}
