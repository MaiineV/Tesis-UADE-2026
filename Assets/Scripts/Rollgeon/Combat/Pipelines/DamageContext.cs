using System;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Data object that travels through every stage of <see cref="DamagePipeline.Resolve"/>.
    /// Callers create it with the input fields; the pipeline fills the output fields.
    /// Specified in TECHNICAL.md §12.2.
    /// </summary>
    public class DamageContext
    {
        // ── Input fields (set by caller) ──────────────────────────────────

        /// <summary>InstanceId of the entity dealing damage.</summary>
        public Guid SourceId;

        /// <summary>InstanceId of the entity receiving damage.</summary>
        public Guid TargetId;

        /// <summary>Raw damage before any pipeline stage.</summary>
        public int BaseDamage;

        /// <summary>Combo id that generated the damage (null/empty if not combo-based).</summary>
        public string ComboId;

        /// <summary>Whether this hit triggers weakness evaluation.</summary>
        public bool IsWeaknessHit;

        /// <summary>Classification of the damage source.</summary>
        public AttackKind Kind;

        // ── Output fields (filled by pipeline) ────────────────────────────

        /// <summary>Damage after weakness multiplier (0 if no weakness).</summary>
        public float WeaknessMultiplier;

        /// <summary>
        /// Stage-3 incoming multiplier applied to this hit. <c>1</c> = nothing modified it, which is
        /// also the value when no <see cref="IIncomingDamageMultiplierProvider"/> is registered.
        /// </summary>
        /// <remarks>
        /// Initialized to <c>1</c> rather than left at <c>default</c>: a consumer reading <c>0</c>
        /// would read "fully absorbed" out of a context the pipeline short-circuited.
        /// </remarks>
        public float IncomingMultiplier = 1f;

        /// <summary>Final damage committed to Health after all stages.</summary>
        public int FinalDamage;

        /// <summary><c>true</c> if the target's Health reached 0 or below.</summary>
        public bool WasLethal;

        /// <summary>Amount of damage absorbed by Shield before applying to Health.</summary>
        public int ShieldAbsorbed;

        /// <summary><c>true</c> if all damage was fully absorbed by shield.</summary>
        public bool BlockedByShield;
    }
}
