using System;
using Rollgeon.Grid;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Conditions
{
    /// <summary>
    /// True si la distancia entre Self y Player es ≤ <see cref="Range"/> según <see cref="Metric"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_PlayerInRange : AICondition
    {
        public enum Distance { Manhattan, Chebyshev }

        [MinValue(0)]
        public int Range = 1;
        public Distance Metric = Distance.Manhattan;

        public override string ConditionName => $"Player in {Metric} range ≤ {Range}";

        public override bool Evaluate(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var self)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var player)) return false;

            int d = Metric == Distance.Manhattan
                ? self.Manhattan(player)
                : self.Chebyshev(player);
            return d <= Range;
        }
    }
}
