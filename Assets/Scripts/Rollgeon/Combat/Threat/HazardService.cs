using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Movement;
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
    /// <para>
    /// Same POCO + <see cref="IPreloadableService"/> pattern as <c>ThreatenedAreaService</c>.
    /// Reuses <see cref="AINode_ExecuteTelegraph"/>/<see cref="AINode_TelegraphMark"/> as-is via a
    /// hand-built <see cref="AIContext"/> per active hazard, once per <c>OnTurnQueueBuilt</c> —
    /// zero telegraph logic duplicated.
    /// </para>
    /// <para>
    /// <b>Two state buckets.</b> <see cref="_active"/> holds cycle-telegraph definitions keyed by
    /// source id (the historical rain path, untouched). <see cref="_instances"/> holds dynamic-area
    /// instances keyed by their own instance id, because <see cref="IThreatenedAreaService"/> stores
    /// one pending area per source — routing several fires from the same SO through it would make
    /// them silently overwrite each other.
    /// </para>
    /// </remarks>
    public sealed class HazardService : IHazardService, IPreloadableService, IDisposable
    {
        private readonly Dictionary<Guid, HazardDefinitionSO> _active = new Dictionary<Guid, HazardDefinitionSO>();
        private readonly Dictionary<Guid, HazardInstance> _instances = new Dictionary<Guid, HazardInstance>();
        private readonly System.Random _rng = new System.Random();

        private EventManager.EventReceiver _onTurnQueueBuiltHandler;
        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        // The exact IMovementService we subscribed to, not just "are we subscribed". That service is
        // run-scoped while this one is Global, so the registered instance can be swapped between
        // runs: `-=` has to target the same object `+=` ran on, and re-resolving from the locator at
        // unsubscribe time could hand us the new instance and leak the old subscription forever.
        private IMovementService _movementSubscribedTo;

        /// <summary>Junto al resto de servicios de combate (ver <c>ThreatenedAreaService.Priority</c> = 80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _onTurnQueueBuiltHandler = OnTurnQueueBuiltExternal;
            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
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
            if (_onTurnFinishedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
                _onTurnFinishedHandler = null;
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
        public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles)
        {
            if (definition == null || tiles == null) return Guid.Empty;

            var set = new HashSet<GridCoord>(tiles);
            if (set.Count == 0) return Guid.Empty;

            // No SourceGuid check here on purpose: an instance is addressed by its own id, so a
            // definition with an unparseable SourceId still works as a dynamic-area hazard.
            var instance = new HazardInstance
            {
                InstanceId = Guid.NewGuid(),
                Definition = definition,
                Tiles = set,
                RemainingRounds = definition.DurationRounds < 0 ? 0 : definition.DurationRounds,
            };
            _instances[instance.InstanceId] = instance;

            EnsureMovementSubscription();
            ShowInstanceOverlay(instance);
            EventManager.Trigger(EventName.OnHazardActivated, instance.InstanceId);
            return instance.InstanceId;
        }

        /// <inheritdoc />
        public bool IsActive(HazardDefinitionSO definition) => definition != null && IsActive(definition.SourceGuid);

        /// <inheritdoc />
        public bool IsActive(Guid sourceId) => sourceId != Guid.Empty && _active.ContainsKey(sourceId);

        /// <inheritdoc />
        public bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info)
        {
            foreach (var instance in _instances.Values)
            {
                if (!instance.Tiles.Contains(coord)) continue;
                info = instance.ToInfo();
                return true;
            }
            info = default;
            return false;
        }

        /// <inheritdoc />
        public IEnumerable<HazardInstanceInfo> ActiveInstances()
        {
            // Materialized, not lazy: callers routinely react to an instance by damaging/consuming
            // it, which mutates the dictionary mid-enumeration.
            var snapshot = new List<HazardInstanceInfo>(_instances.Count);
            foreach (var instance in _instances.Values)
                snapshot.Add(instance.ToInfo());
            return snapshot;
        }

        /// <inheritdoc />
        public void Deactivate(Guid instanceId) => ExpireInstance(instanceId);

        /// <inheritdoc />
        public void SkipNextTick(Guid instanceId)
        {
            if (_instances.TryGetValue(instanceId, out var instance))
                instance.SkipNextTick = true;
        }

        // ======================================================================
        // Internals — lifecycle
        // ======================================================================

        private void ResetAll()
        {
            UnsubscribeMovement();

            bool hasThreat = ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) && threat != null;
            bool hasOverlay = ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null;

            foreach (var sourceId in _active.Keys)
            {
                if (hasThreat) threat.Clear(sourceId);
                if (hasOverlay) overlay.Clear(sourceId);
            }
            _active.Clear();

            // No OnHazardExpired here: same call as ComboBlockService.Clear, which deliberately
            // stays quiet on scope teardown so listeners don't run "the fire went out" reactions
            // while combat is already over.
            foreach (var instanceId in _instances.Keys)
            {
                if (hasOverlay) overlay.Clear(instanceId);
            }
            _instances.Clear();
        }

        private void ExpireInstance(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return;
            if (!_instances.Remove(instanceId)) return;

            ClearOverlay(instanceId);
            EventManager.Trigger(EventName.OnHazardExpired, instanceId);
        }

        private void EnsureMovementSubscription()
        {
            if (_movementSubscribedTo != null) return;
            if (!HasInstanceWithTrigger(HazardTriggerMode.OnEnter)) return;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null) return;

            movement.OnEntityMoved += OnEntityMovedExternal;
            _movementSubscribedTo = movement;
        }

        private void UnsubscribeMovement()
        {
            if (_movementSubscribedTo == null) return;

            _movementSubscribedTo.OnEntityMoved -= OnEntityMovedExternal;
            _movementSubscribedTo = null;
        }

        private bool HasInstanceWithTrigger(HazardTriggerMode trigger)
        {
            foreach (var instance in _instances.Values)
                if (instance.Definition != null && instance.Definition.Trigger == trigger) return true;
            return false;
        }

        // ======================================================================
        // Internals — ticking
        // ======================================================================

        private void TickInstanceDurations()
        {
            if (_instances.Count == 0) return;

            List<Guid> expired = null;
            foreach (var instance in _instances.Values)
            {
                if (instance.RemainingRounds <= 0) continue; // 0 = never expires on its own.

                instance.RemainingRounds--;
                if (instance.RemainingRounds <= 0)
                    (expired ??= new List<Guid>()).Add(instance.InstanceId);
            }

            if (expired == null) return;
            foreach (var instanceId in expired)
                ExpireInstance(instanceId);
        }

        private void TickCycleTelegraphs(int roundIndex)
        {
            if (_active.Count == 0) return;

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

        // ======================================================================
        // Internals — effects
        // ======================================================================

        private static void ShowInstanceOverlay(HazardInstance instance)
        {
            if (instance.Tiles.Count == 0) return;

            ThreatTelegraphOverlay.ResolveOrCreate()
                .Show(instance.InstanceId, instance.Tiles, instance.Definition.EffectiveOverlayTint);
        }

        private static void ClearOverlay(Guid key)
        {
            // TryGet, not ResolveOrCreate: clearing must never be the reason an overlay (and its
            // scene GameObject) springs into existence.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(key);
        }

        private static void ApplyHazardDamage(HazardInstance instance, Guid targetGuid)
        {
            int damage = instance.Definition.Damage;
            if (damage <= 0) return; // Ice deals no damage — it only needs to raise the trigger event.
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null) return;

            pipeline.Resolve(new DamageContext
            {
                SourceId = instance.InstanceId,
                TargetId = targetGuid,
                BaseDamage = damage,
                Kind = instance.Definition.Kind,
            });
        }

        /// <summary>
        /// Applies one hazard hit: damage (if any), the trigger event, and tile consumption. The
        /// event is what carries the effect other systems layer on top — the ice stun lives in
        /// StunService listening to <c>OnHazardTriggered</c>, not here.
        /// </summary>
        private void TriggerInstance(HazardInstance instance, Guid entityGuid, GridCoord coord)
        {
            ApplyHazardDamage(instance, entityGuid);
            EventManager.Trigger(EventName.OnHazardTriggered, instance.InstanceId, entityGuid);

            if (!instance.Definition.ConsumeOnTrigger) return;
            if (!instance.Tiles.Remove(coord)) return;

            if (instance.Tiles.Count == 0)
            {
                ExpireInstance(instance.InstanceId);
                return;
            }
            ShowInstanceOverlay(instance); // Repaint without the spent tile.
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnScopeEndedExternal(params object[] args) => ResetAll();

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is int roundIndex)) return;
            if (roundIndex <= 0) return;

            // Retry here too: an OnEnter hazard can be activated before IMovementService is
            // registered (or after a run swapped it out), and this is the cheapest recurring hook.
            EnsureMovementSubscription();

            TickInstanceDurations();
            TickCycleTelegraphs(roundIndex);
        }

        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid entityGuid) || entityGuid == Guid.Empty) return;
            if (_instances.Count == 0) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!grid.TryGetPosition(entityGuid, out var coord)) return;

            foreach (var instance in new List<HazardInstance>(_instances.Values))
            {
                if (instance.Definition == null) continue;
                if (instance.Definition.Trigger != HazardTriggerMode.OnTurnEndInTile) continue;
                if (!instance.Tiles.Contains(coord)) continue;

                if (instance.SkipNextTick)
                {
                    // Consumed only by a tick that would actually have landed, so a boss can arm it
                    // during its own turn without knowing yet whether anyone is standing in the fire.
                    instance.SkipNextTick = false;
                    continue;
                }

                TriggerInstance(instance, entityGuid, coord);
            }
        }

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (entity == Guid.Empty || _instances.Count == 0) return;

            foreach (var coord in EnteredTiles(from, to, path))
            {
                // Re-snapshot per step: a trigger can consume tiles or expire an instance mid-scan.
                foreach (var instance in new List<HazardInstance>(_instances.Values))
                {
                    if (instance.Definition == null) continue;
                    if (instance.Definition.Trigger != HazardTriggerMode.OnEnter) continue;
                    if (!instance.Tiles.Contains(coord)) continue;

                    TriggerInstance(instance, entity, coord);
                }
            }
        }

        /// <summary>
        /// Tiles the entity actually stepped <i>into</i>, in travel order — the whole path minus the
        /// origin, so walking through a trap triggers it instead of only landing on one.
        /// </summary>
        private static IEnumerable<GridCoord> EnteredTiles(GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (path == null || path.Count == 0)
            {
                // Teleports / instant repositioning report no path, but the destination is still an entry.
                if (!to.Equals(from)) yield return to;
                yield break;
            }

            // IMovementService paths include the origin (see IMovementService.FindPath). Dropping it
            // by value rather than by index keeps this correct whichever end it is serialized at.
            foreach (var coord in path)
            {
                if (!coord.Equals(from)) yield return coord;
            }
        }

        // ======================================================================
        // Instance state
        // ======================================================================

        /// <summary>Mutable runtime state of one dynamic-area hazard activation.</summary>
        private sealed class HazardInstance
        {
            public Guid InstanceId;
            public HazardDefinitionSO Definition;
            public HashSet<GridCoord> Tiles;

            /// <summary>Rounds left before expiry; <c>0</c> means "never expires".</summary>
            public int RemainingRounds;

            /// <summary>One-shot suppression armed by <see cref="IHazardService.SkipNextTick"/>.</summary>
            public bool SkipNextTick;

            /// <summary>Copies the tile set — the info struct promises an immutable snapshot.</summary>
            public HazardInstanceInfo ToInfo()
                => new HazardInstanceInfo(InstanceId, Definition, new List<GridCoord>(Tiles), RemainingRounds);
        }
    }
}
