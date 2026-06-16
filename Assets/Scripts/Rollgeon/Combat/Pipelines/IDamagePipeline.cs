namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Contract for the centralized damage pipeline (TECHNICAL.md §12.2).
    /// All damage in the game flows through an implementation of this interface.
    /// </summary>
    public interface IDamagePipeline
    {
        /// <summary>
        /// Resolves damage from source to target, applying weakness, writing to Health,
        /// and firing events. Returns the <see cref="DamageContext"/> with output fields filled.
        /// </summary>
        DamageContext Resolve(DamageContext ctx);
    }
}
