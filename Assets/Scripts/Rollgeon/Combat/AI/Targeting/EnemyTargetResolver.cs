using System;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Helper estático que aplica la regla "selector null → fallback default
    /// (<see cref="TargetSelector_AlwaysPlayer"/>)". Replica la semántica de
    /// <c>BasePreCondition.EvaluateAll</c> (lista vacía / null = no-op) para que
    /// los autores puedan dejar el campo del selector en blanco con un default sensato.
    /// </summary>
    public static class EnemyTargetResolver
    {
        private static readonly TargetSelector_AlwaysPlayer DefaultSelector = new TargetSelector_AlwaysPlayer();

        public static Guid Resolve(BaseEnemyTargetSelector selector, AIContext ctx, Guid ownerGuid)
        {
            var pick = selector ?? DefaultSelector;
            return pick.PickTarget(ctx, ownerGuid);
        }
    }
}
