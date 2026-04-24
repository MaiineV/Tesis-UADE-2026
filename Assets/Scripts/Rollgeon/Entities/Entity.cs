using System;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Minimal runtime entity handle. Consumed by <see cref="Rollgeon.Effects.EffectContext"/>,
    /// <see cref="Rollgeon.PreConditions.PreConditionContext"/>, and target queries.
    /// </summary>
    public class Entity
    {
        public Guid Guid;
    }
}
