using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Generalization of the old <see cref="RainHazardService"/>: runs any number of active
    /// <see cref="HazardDefinitionSO"/> in parallel, each on its own cadence and its own
    /// <see cref="IThreatenedAreaService"/>/<see cref="IThreatOverlayService"/> source id. Adding a
    /// new hazard type is "author a definition SO, point an <see cref="AINode_ActivateHazard"/> at
    /// it" — no new service code required.
    /// </summary>
    /// <remarks>
    /// Same POCO + <see cref="IPreloadableService"/> pattern as <c>ThreatenedAreaService</c>.
    /// Reuses <see cref="AINode_ExecuteTelegraph"/>/<see cref="AINode_TelegraphMark"/> as-is via a
    /// hand-built <see cref="AIContext"/> per active hazard, once per <c>OnTurnQueueBuilt</c> —
    /// zero telegraph logic duplicated.
    /// </remarks>
    public sealed class HazardService : IHazardService, IPreloadableService, IDisposable
    {
        private readonly Dictionary<Guid, HazardDefinitionSO> _active = new Dictionary<Guid, HazardDefinitionSO>();
        private readonly System.Random _rng = new System.Random();

        private EventManager.EventReceiver _onTurnQueueBuiltHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto al resto de servicios de combate (ver <c>ThreatenedAreaService.Priority</c> = 80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _onTurnQueueBuiltHandler = OnTurnQueueBuiltExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IHazardService>(this, ServiceScope.Global);
            ServiceLocator.AddService<HazardService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onTurnQueueBuiltHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
                _onTurnQueueBuiltHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            ResetAll();
        }

        // ======================================================================
        // IHazardService
        // ======================================================================

        /// <inheritdoc />
        public void Activate(HazardDefinitionSO definition)
        {
            if (definition == null) return;

            var id = definition.SourceGuid;
            if (id == Guid.Empty) return; // HazardDefinitionSO.SourceGuid already logged the parse error.
            if (_active.ContainsKey(id)) return; // idempotent

            _active[id] = definition;
        }

        /// <inheritdoc />
        public bool IsActive(HazardDefinitionSO definition) => definition != null && IsActive(definition.SourceGuid);

        /// <inheritdoc />
        public bool IsActive(Guid sourceId) => sourceId != Guid.Empty && _active.ContainsKey(sourceId);

        // ======================================================================
        // Internals
        // ======================================================================

        private void ResetAll()
        {
            if (_active.Count == 0) return;

            bool hasThreat = ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) && threat != null;
            bool hasOverlay = ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null;

            foreach (var sourceId in _active.Keys)
            {
                if (hasThreat) threat.Clear(sourceId);
                if (hasOverlay) overlay.Clear(sourceId);
            }
            _active.Clear();
        }

        private void OnScopeEndedExternal(params object[] args) => ResetAll();

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            if (_active.Count == 0) return;
            if (args == null || args.Length < 2 || !(args[1] is int roundIndex)) return;
            if (roundIndex <= 0) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var damagePipeline) || damagePipeline == null) return;

            // Snapshot: nothing today activates/deactivates a hazard from inside this loop, but a
            // future hazard could (e.g. one that deactivates itself on trigger) — iterating a copy
            // avoids a "collection modified while enumerating" landmine down the line.
            foreach (var definition in new List<HazardDefinitionSO>(_active.Values))
            {
                int cycle = definition.CycleRounds < 1 ? 1 : definition.CycleRounds;
                if (roundIndex % cycle != 0) continue;

                var ctx = new AIContext
                {
                    SelfGuid = definition.SourceGuid,
                    PlayerGuid = playerService.PlayerGuid,
                    Grid = grid,
                    DamagePipeline = damagePipeline,
                    Rng = _rng,
                };

                new AINode_ExecuteTelegraph().Tick(ctx);
                new AINode_TelegraphMark
                {
                    Shape = definition.Shape,
                    Size = definition.Size,
                    Depth = definition.Depth,
                    Count = definition.Count,
                    HalfAxis = definition.HalfAxis,
                    Damage = definition.Damage,
                    Kind = definition.Kind,
                }.Tick(ctx);
            }
        }
    }
}
