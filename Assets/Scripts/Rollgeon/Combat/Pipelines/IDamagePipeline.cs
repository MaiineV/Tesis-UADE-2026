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

        /// <summary>
        /// Read-only computation of <see cref="DamageContext.FinalDamage"/> (weakness multiplier +
        /// shield absorption) for previews. Writes nothing (no Health/Shield mutation) and fires no
        /// events. Uses the same mitigation math as <see cref="Resolve"/> so the preview never drifts.
        /// </summary>
        DamageContext Preview(DamageContext ctx);
    }
}
