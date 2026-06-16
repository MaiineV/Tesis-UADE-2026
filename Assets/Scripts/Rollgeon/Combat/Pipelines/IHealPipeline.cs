namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Contract for the centralized heal pipeline.
    /// All healing in the game flows through an implementation of this interface.
    /// </summary>
    public interface IHealPipeline
    {
        /// <summary>
        /// Resolves healing from source to target, applying clamps, writing to Health,
        /// and firing events. Returns the <see cref="HealContext"/> with output fields filled.
        /// </summary>
        HealContext Resolve(HealContext ctx);
    }
}
