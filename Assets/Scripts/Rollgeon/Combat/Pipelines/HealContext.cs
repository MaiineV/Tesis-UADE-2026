using System;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Data object that travels through every stage of <see cref="HealPipeline.Resolve"/>.
    /// Callers create it with the input fields; the pipeline fills the output fields.
    /// </summary>
    public class HealContext
    {
        // -- Input fields (set by caller) ------------------------------------------

        /// <summary>InstanceId of the entity providing the heal.</summary>
        public Guid SourceId;

        /// <summary>InstanceId of the entity receiving the heal.</summary>
        public Guid TargetId;

        /// <summary>Raw heal amount before any pipeline stage.</summary>
        public int BaseHeal;

        /// <summary>Free-form label e.g. "potion", "support.heal".</summary>
        public string SourceTag;

        /// <summary>If true, BaseHeal is treated as a percentage of max HP.</summary>
        public bool IsPercentOfMax;

        // -- Output fields (filled by pipeline) ------------------------------------

        /// <summary>Final heal committed to Health after all stages.</summary>
        public int FinalHeal;

        /// <summary><c>true</c> if the heal was reduced by the max HP cap.</summary>
        public bool WasClamped;
    }
}
