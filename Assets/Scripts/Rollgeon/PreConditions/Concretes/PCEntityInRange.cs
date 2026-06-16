using System;
using Patterns;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    public enum DistanceMetric
    {
        Manhattan = 0,
        Chebyshev = 1,
    }

    /// <summary>
    /// Chequea que <see cref="PreConditionContext.OpponentGuid"/> esté a ≤
    /// <see cref="MaxRange"/> tiles del <see cref="PreConditionContext.OwnerGuid"/>.
    /// TECHNICAL.md §8.2.
    /// <para>
    /// Si owner u opponent no están registrados en el <see cref="IGridManager"/>
    /// (sala no cargada, entity sin posición), evalúa <c>false</c>. Manhattan
    /// es el default para grilla 4-connected.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCEntityInRange : BasePreCondition
    {
        [Tooltip("Rango máximo en tiles (inclusive). 0 = mismo tile.")]
        [MinValue(0)]
        public int MaxRange = 1;

        [Tooltip("Manhattan: |dx|+|dy| (4-grid). Chebyshev: max(|dx|,|dy|) (8-grid).")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        public override string ConditionName => $"InRange({Metric}, ≤{MaxRange})";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return false;
            if (context.OwnerGuid == Guid.Empty || context.OpponentGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;

            if (!grid.TryGetPosition(context.OwnerGuid, out var ownerCoord)) return false;
            if (!grid.TryGetPosition(context.OpponentGuid, out var opponentCoord)) return false;

            int dist = Metric == DistanceMetric.Manhattan
                ? ownerCoord.Manhattan(opponentCoord)
                : ownerCoord.Chebyshev(opponentCoord);

            return dist <= MaxRange;
        }
    }
}
