namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Polymorphic base for trigger contexts that feed into a <see cref="BaseBehavior"/>.
    /// TECHNICAL.md 7.3. Concrete subtypes: <c>HeroBehaviorContext</c>,
    /// <c>DamageBehaviorContext</c>, <c>TurnBehaviorContext</c>, etc.
    /// </summary>
    public abstract class BehaviorContext
    {
        public Rollgeon.Entities.Entity SourceEntity;
        public Rollgeon.Entities.Entity TriggeringEntity;
    }
}
