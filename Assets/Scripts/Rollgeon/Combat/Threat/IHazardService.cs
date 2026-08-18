using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Generic environmental-hazard runner (rain, fire, ice, falling debris, ...): any number of
    /// <see cref="HazardDefinitionSO"/> can be active at once, each ticking on its own
    /// <see cref="HazardDefinitionSO.CycleRounds"/> cadence via its own
    /// <see cref="HazardDefinitionSO.SourceGuid"/>.
    /// </summary>
    /// <remarks>
    /// <b>Two flavours of "active".</b> <see cref="Activate(HazardDefinitionSO)"/> registers one
    /// cycle-run per <see cref="HazardDefinitionSO.SourceGuid"/> — what <see cref="IsActive(Guid)"/>
    /// reports. <see cref="Activate(HazardDefinitionSO, IEnumerable{GridCoord})"/> creates an
    /// independent <b>instance</b>, addressed only by the <c>instanceId</c> it returns.
    /// </remarks>
    public interface IHazardService
    {
        /// <summary>
        /// Activates <paramref name="definition"/>. Idempotent per
        /// <see cref="HazardDefinitionSO.SourceGuid"/>; no-op when the definition is null or its
        /// SourceId doesn't parse to a valid GUID.
        /// </summary>
        void Activate(HazardDefinitionSO definition);

        /// <summary>
        /// Activates <paramref name="definition"/> over an explicit <paramref name="tiles"/> set,
        /// <b>ignoring</b> its <see cref="HazardDefinitionSO.Shape"/>, and returns the new instance
        /// id. Each call creates an independent instance. Returns <see cref="Guid.Empty"/> and does
        /// nothing when <paramref name="definition"/> is null or <paramref name="tiles"/> is empty.
        /// </summary>
        Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles);

        /// <summary><c>true</c> if <paramref name="definition"/> (by its SourceGuid) is active.</summary>
        bool IsActive(HazardDefinitionSO definition);

        /// <summary>
        /// Source-keyed: never answers for an <c>instanceId</c> — use <see cref="ActiveInstances"/>
        /// or <see cref="TryGetHazardAt"/> for those.
        /// </summary>
        bool IsActive(Guid sourceId);

        /// <summary>
        /// Finds an active instance covering <paramref name="coord"/>, first match wins. Only
        /// dynamic-area instances: cycle-telegraph definitions keep their tiles in
        /// <see cref="IThreatenedAreaService"/>.
        /// </summary>
        bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info);

        /// <summary>Snapshot of every live dynamic-area instance. Safe to enumerate while mutating.</summary>
        IEnumerable<HazardInstanceInfo> ActiveInstances();

        /// <summary>
        /// Kills the instance early, clearing its overlay and raising <c>OnHazardExpired</c>.
        /// </summary>
        void Deactivate(Guid instanceId);

        /// <summary>
        /// Arms a one-shot suppression on <paramref name="instanceId"/>: the next turn-end tick that
        /// <i>would have</i> damaged someone is swallowed instead, and the flag clears itself.
        /// </summary>
        /// <remarks>
        /// Does <b>not</b> suppress duration ticking: the hazard still ages.
        /// <see cref="HazardDefinitionSO.Affects"/> is not a replacement — that filter answers "is
        /// this entity billable at all", this one "did the billable entity already pay for this tile
        /// this turn". Drop it and the Croupier's detonated sector charges the blast plus the fire on
        /// one turn end: 26 instead of 20, with
        /// <c>CroupierIgnitionTests.PlayerCaughtByTheBlast_FirstFireTickIsSwallowed</c> as the alarm.
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
