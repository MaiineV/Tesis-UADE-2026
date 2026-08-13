namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// How a <see cref="HazardDefinitionSO"/> decides when to hurt someone. Split out of the
    /// definition because the three modes drive three completely different subscriptions in
    /// <see cref="HazardService"/> (round wrap, turn end, movement) — not just a damage tweak.
    /// </summary>
    public enum HazardTriggerMode
    {
        /// <summary>
        /// Telegraph the shape on one cycle, resolve it on the next (rain). The historical — and
        /// still default — behavior: driven by <see cref="HazardDefinitionSO.CycleRounds"/> off the
        /// turn-queue round index, using the definition's own <c>Shape</c>.
        /// </summary>
        CycleTelegraph,

        /// <summary>
        /// Damages whoever <b>ends their turn</b> standing on one of the hazard's tiles (fire).
        /// Requires a dynamic area — see the tiles overload of <see cref="IHazardService.Activate"/>.
        /// </summary>
        OnTurnEndInTile,

        /// <summary>
        /// Fires the moment an entity <b>steps onto</b> one of the hazard's tiles (ice), scanning
        /// the whole movement path rather than just the destination, so you can't dash across a
        /// trap for free. Requires a dynamic area.
        /// </summary>
        OnEnter,
    }
}
