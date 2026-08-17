using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Generic environmental-hazard runner (rain, fire, ice, falling debris, ...). Replaces the
    /// rain-only <see cref="RainHazardService"/> loop with a data-driven one: any number of
    /// <see cref="HazardDefinitionSO"/> can be active at once, each ticking on its own
    /// <see cref="HazardDefinitionSO.CycleRounds"/> cadence via its own
    /// <see cref="HazardDefinitionSO.SourceGuid"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two flavours of "active".</b> <see cref="Activate(HazardDefinitionSO)"/> registers a
    /// definition to run on its cycle using its own <c>Shape</c> — one per
    /// <see cref="HazardDefinitionSO.SourceGuid"/>, which is what <see cref="IsActive(Guid)"/>
    /// reports. <see cref="Activate(HazardDefinitionSO, IEnumerable{GridCoord})"/> instead creates an
    /// independent <b>instance</b> over an explicit tile set, and any number of instances of the same
    /// definition can coexist (one fire per detonated sector). Instances are addressed by the
    /// <c>instanceId</c> the overload returns — never by source id.
    /// </para>
    /// <para>
    /// <b>Instances don't go through <see cref="IThreatenedAreaService"/>.</b> That service holds one
    /// pending area per source and is built around mark-now/consume-next-turn; instances need
    /// persistent, per-instance tile membership instead. Keeping them in this service is what lets
    /// two fires from the same SO stop overwriting each other.
    /// </para>
    /// </remarks>
    public interface IHazardService
    {
        /// <summary>
        /// Activates <paramref name="definition"/> (idempotent — activating an already-active
        /// definition, or another instance sharing its <see cref="HazardDefinitionSO.SourceGuid"/>,
        /// is a no-op). No-op if <paramref name="definition"/> is null or its SourceId doesn't
        /// parse to a valid GUID.
        /// </summary>
        void Activate(HazardDefinitionSO definition);

        /// <summary>
        /// Activates <paramref name="definition"/> over an explicit <paramref name="tiles"/> set,
        /// <b>ignoring</b> its <see cref="HazardDefinitionSO.Shape"/>, and returns the id of the new
        /// instance. Each call creates an independent instance with its own tiles and its own
        /// remaining duration, so the same definition can be active several times over.
        /// Returns <see cref="Guid.Empty"/> (and does nothing) if <paramref name="definition"/> is
        /// null or <paramref name="tiles"/> is null/empty.
        /// </summary>
        Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles);

        /// <summary><c>true</c> if <paramref name="definition"/> (by its SourceGuid) is active.</summary>
        bool IsActive(HazardDefinitionSO definition);

        /// <summary>
        /// <c>true</c> if the hazard whose SourceGuid is <paramref name="sourceId"/> is active.
        /// Source-keyed by design: this never answers for an <c>instanceId</c> — use
        /// <see cref="ActiveInstances"/> or <see cref="TryGetHazardAt"/> for those.
        /// </summary>
        bool IsActive(Guid sourceId);

        /// <summary>
        /// Finds an active instance covering <paramref name="coord"/>. Only reports dynamic-area
        /// instances — cycle-telegraph definitions keep their tiles in
        /// <see cref="IThreatenedAreaService"/>, not here. First match wins when areas overlap.
        /// </summary>
        bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info);

        /// <summary>Snapshot of every live dynamic-area instance. Safe to enumerate while mutating.</summary>
        IEnumerable<HazardInstanceInfo> ActiveInstances();

        /// <summary>
        /// Kills the instance <paramref name="instanceId"/> early (clearing its overlay and raising
        /// <c>OnHazardExpired</c>). No-op for an unknown id.
        /// </summary>
        void Deactivate(Guid instanceId);

        /// <summary>
        /// Arms a one-shot suppression on <paramref name="instanceId"/>: the next turn-end tick that
        /// <i>would have</i> damaged someone is swallowed instead, and the flag clears itself.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Exists for the design rule "the detonation consumes the flame" — a boss node that already
        /// resolved damage over a tile this turn calls this so the standing fire doesn't bill the
        /// player twice for the same turn. Deliberately does not suppress duration ticking: the
        /// hazard still ages.
        /// </para>
        /// <para>
        /// <b><see cref="HazardDefinitionSO.Affects"/> does not replace this, and deleting it is not a
        /// cleanup.</b> That filter answers "is this entity billable at all"; this answers "did the
        /// billable entity already pay for this tile this turn". Both are about the player and they
        /// never overlap. Drop it and the Croupier's seam column goes back to charging the blast plus
        /// the fire's 6 on one turn end — worst case 30 instead of 24, with
        /// <c>CroupierIgnitionTests.PlayerCaughtByTheBlast_FirstFireTickIsSwallowed</c> as the alarm.
        /// (The commit that added <c>Affects</c> claimed this had become redundant. It had not.)
        /// </para>
        /// </remarks>
        void SkipNextTick(Guid instanceId);
    }

    /// <summary>Immutable snapshot of one live dynamic-area hazard instance.</summary>
    public readonly struct HazardInstanceInfo
    {
        public readonly Guid InstanceId;
        public readonly HazardDefinitionSO Definition;
        public readonly IReadOnlyCollection<GridCoord> Tiles;

        /// <summary>Rounds left before the instance expires; <c>0</c> means "never expires".</summary>
        public readonly int RemainingRounds;

        public HazardInstanceInfo(
            Guid instanceId, HazardDefinitionSO definition, IReadOnlyCollection<GridCoord> tiles, int remainingRounds)
        {
            InstanceId = instanceId;
            Definition = definition;
            Tiles = tiles;
            RemainingRounds = remainingRounds;
        }
    }
}
