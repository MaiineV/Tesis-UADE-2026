namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// How a <see cref="HazardDefinitionSO"/> decides when to hurt someone. Each mode drives a
    /// different subscription in <see cref="HazardService"/>: round wrap, turn end, movement.
    /// </summary>
    public enum HazardTriggerMode
    {
        /// <summary>
        /// Telegraph the shape on one cycle, resolve it on the next (rain): driven by
        /// <see cref="HazardDefinitionSO.CycleRounds"/> off the turn-queue round index, using the
        /// definition's own <c>Shape</c>.
        /// </summary>
        CycleTelegraph,

        /// <summary>
        /// Damages whoever <b>ends their turn</b> standing on one of the hazard's tiles (fire),
        /// narrowed by <see cref="HazardDefinitionSO.Affects"/>. Requires a dynamic area — see the
        /// tiles overload of <see cref="IHazardService.Activate"/>.
        /// </summary>
        OnTurnEndInTile,

        /// <summary>
        /// Fires the moment an eligible entity <b>steps onto</b> one of the hazard's tiles (ice),
        /// scanning the whole movement path rather than just the destination, so you can't dash
        /// across a trap for free. Eligibility is <see cref="HazardDefinitionSO.Affects"/>. Requires
        /// a dynamic area.
        /// </summary>
        OnEnter,
    }
}
