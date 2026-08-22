namespace Rollgeon.Combat.Threat
{
    /// <summary>Who a <see cref="HazardDefinitionSO"/> is allowed to bill.</summary>
    public enum HazardAffects
    {
        /// <summary>
        /// Only the player pays. Without this a boss that covers ground with its own hazard ends its
        /// turn inside it and hurts itself.
        /// </summary>
        PlayerOnly,

        /// <summary>Anything that steps in or ends its turn inside pays, the player included.</summary>
        Everyone,
    }
}
