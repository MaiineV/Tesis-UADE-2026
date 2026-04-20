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

        /// <summary>Final damage committed to Health after all stages.</summary>
        public int FinalDamage;

        /// <summary><c>true</c> if the target's Health reached 0 or below.</summary>
        public bool WasLethal;
    }
}
