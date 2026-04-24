namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Keys for the runtime stored-value bag on <see cref="BaseBehavior"/>.
    /// TECHNICAL.md 9.2. Stability rule: never renumber, only append.
    /// </summary>
    public enum BehaviorValueKey
    {
        None = 0,
        FloatingDamage = 1,
        FloatingHeal = 2,
        FloatingShield = 3,
    }
}
