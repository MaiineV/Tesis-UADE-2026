namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Who a <see cref="HazardDefinitionSO"/> is allowed to bill. Split out of the definition for the
    /// same reason as <see cref="HazardTriggerMode"/>: it changes who <see cref="HazardService"/>
    /// answers to, not how much the hit hurts.
    /// </summary>
    public enum HazardAffects
    {
        /// <summary>
        /// Only the player pays. The default, and the right answer for every hazard authored so far:
        /// a burning table or a frozen tile is pressure applied <i>to the player</i>, not physics the
        /// room runs on everyone. Without this, a boss that covers ground with its own hazard ends
        /// its turn inside it and hurts itself — which is how the Croupier came to burn in his own
        /// fire once his row became part of a sector.
        /// </summary>
        PlayerOnly,

        /// <summary>
        /// Anything that steps in or ends its turn inside pays, the player included. Nothing uses
        /// this today; it exists so a hazard that genuinely belongs to the room (a collapsing floor,
        /// a shared arena effect) can opt in without reopening <see cref="HazardService"/>.
        /// </summary>
        Everyone,
    }
}
