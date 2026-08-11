using System;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Generic environmental-hazard runner (rain, fire, falling debris, ...). Replaces the
    /// rain-only <see cref="RainHazardService"/> loop with a data-driven one: any number of
    /// <see cref="HazardDefinitionSO"/> can be active at once, each ticking on its own
    /// <see cref="HazardDefinitionSO.CycleRounds"/> cadence via its own
    /// <see cref="HazardDefinitionSO.SourceGuid"/>.
    /// </summary>
    public interface IHazardService
    {
        /// <summary>
        /// Activates <paramref name="definition"/> (idempotent — activating an already-active
        /// definition, or another instance sharing its <see cref="HazardDefinitionSO.SourceGuid"/>,
        /// is a no-op). No-op if <paramref name="definition"/> is null or its SourceId doesn't
        /// parse to a valid GUID.
        /// </summary>
        void Activate(HazardDefinitionSO definition);

        /// <summary><c>true</c> if <paramref name="definition"/> (by its SourceGuid) is active.</summary>
        bool IsActive(HazardDefinitionSO definition);

        /// <summary><c>true</c> if the hazard whose SourceGuid is <paramref name="sourceId"/> is active.</summary>
        bool IsActive(Guid sourceId);
    }
}
