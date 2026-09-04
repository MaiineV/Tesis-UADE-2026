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
using UnityEngine;
// Sin el alias, `Object` resolvería a System.Object y los helpers de escena no compilarían.
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Runs any number of active <see cref="HazardDefinitionSO"/> in parallel, each on its own
    /// cadence and source id.
    /// </summary>
    /// <remarks>
    /// <b>Two state buckets.</b> <see cref="_active"/> keys cycle-telegraph definitions by source
    /// id; <see cref="_instances"/> keys dynamic-area instances by instance id, because
    /// <see cref="IThreatenedAreaService"/> stores one pending area per source — several fires from
    /// the same SO would silently overwrite each other there.
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

        // The exact IMovementService we subscribed to: it is run-scoped while this service is Global,
        // so re-resolving at unsubscribe time could hand us a newer instance and leak the old one.
        private IMovementService _movementSubscribedTo;

        private bool _warnedMissingPlayerService;

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
        public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles,
                             Guid ownerGuid = default)
        {
            if (definition == null || tiles == null) return Guid.Empty;

            var set = new HashSet<GridCoord>(tiles);
            if (set.Count == 0) return Guid.Empty;

            // No SourceGuid check: an instance is addressed by its own id, so a definition with an
            // unparseable SourceId still works as a dynamic-area hazard.
            var instance = new HazardInstance
            {
                InstanceId = Guid.NewGuid(),
                Definition = definition,
                Tiles = set,
                OwnerGuid = ownerGuid,
                RemainingRounds = definition.DurationRounds < 0 ? 0 : definition.DurationRounds,
            };
            _instances[instance.InstanceId] = instance;

            EnsureMovementSubscription();
            ShowInstanceOverlay(instance);
            SpawnPersistentVfx(instance);
            SpawnHoverAnchor(instance);
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
            // Materialized, not lazy: callers react to an instance by damaging/consuming it, which
            // mutates the dictionary mid-enumeration.
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

            // No OnHazardExpired on teardown: listeners must not run "the fire went out" reactions
            // while combat is already over.
            foreach (var pair in _instances)
            {
                if (hasOverlay) overlay.Clear(pair.Key);
                // El VFX y el hover sí se apagan: son GameObjects en escena y sobreviven al
                // teardown solos.
                ClearPersistentVfx(pair.Value);
                ClearHoverAnchor(pair.Value);
            }
            _instances.Clear();
        }

        private void ExpireInstance(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return;
            if (!_instances.TryGetValue(instanceId, out var instance)) return;
            _instances.Remove(instanceId);

            ClearPersistentVfx(instance);
            ClearHoverAnchor(instance);
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

            // Snapshot: a hazard that deactivates itself on trigger would otherwise modify the
            // collection mid-enumeration.
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
            // TryGet, not ResolveOrCreate: clearing must never be the reason an overlay springs
            // into existence.
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

        /// <summary>No-op sin <see cref="HazardDefinitionSO.PersistentVfxPrefab"/>.</summary>
        private static void SpawnPersistentVfx(HazardInstance instance)
        {
            var prefab = instance?.Definition?.PersistentVfxPrefab;
            if (prefab == null) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[HazardService] IGridManager no registrado — el hazard vive igual, " +
                                 "pero sin llama persistente (no hay con qué ubicar la tile).");
                return;
            }

            foreach (var coord in instance.Tiles)
            {
                if (instance.PersistentVfx.ContainsKey(coord)) continue;

                var world = grid.GridToWorld(coord)
                            + Vector3.up * instance.Definition.PersistentVfxYOffset;
                var go = Object.Instantiate(prefab, world, Quaternion.identity);
                go.name = $"{prefab.name} (hazard {coord})";
                instance.PersistentVfx[coord] = go;
            }
        }

        /// <summary>
        /// El hover de la instancia: una raíz con el tooltip del panel (header + card de golpe) y
        /// un collider bajo y chato por casilla — los quads del overlay no pueden llevarlo (lo
        /// prohíbe el TileClickHandler), así que el hover vive en objetos propios, como el de las
        /// casillas especiales. No-op sin <see cref="IGridManager"/>: el hazard cobra igual, mudo.
        /// </summary>
        private static void SpawnHoverAnchor(HazardInstance instance)
        {
            if (instance.HoverAnchor != null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;

            var root = new GameObject($"HazardHover ({instance.Definition.name})");
            var info = root.AddComponent<HazardTooltipInfo>();
            info.Bind(instance.Definition, instance.InstanceId, instance.OwnerGuid);

            var trigger = root.AddComponent<Rollgeon.UI.Tooltips.WorldTooltipTrigger>();
            trigger.Mode = Rollgeon.UI.Tooltips.WorldTooltipMode.Hover;
            trigger.Placement = Rollgeon.UI.Tooltips.TooltipPlacementMode.ScreenTopRight;
            trigger.ContentProvider = info.BuildContent;
            trigger.CardsProvider = info.CollectCards;

            instance.HoverAnchor = root;
            foreach (var coord in instance.Tiles)
                SpawnHoverCell(instance, grid, coord);
        }

        private static void SpawnHoverCell(HazardInstance instance, IGridManager grid, GridCoord coord)
        {
            if (instance.HoverCells.ContainsKey(coord)) return;

            var cell = new GameObject($"cell {coord}");
            cell.transform.SetParent(instance.HoverAnchor.transform, worldPositionStays: false);
            cell.transform.position = grid.GridToWorld(coord);

            // Bajo y chato, calcado de SpecialTileVisualPool: acusa el hover sin molestar al
            // input (el pick de celda intersecta el plano del piso).
            var collider = cell.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.9f, 0.1f, 0.9f);
            collider.center = new Vector3(0f, 0.05f, 0f);

            instance.HoverCells[coord] = cell;
        }

        /// <summary>Suelta una casilla, o la raíz entera con <paramref name="coord"/> en <c>null</c>.</summary>
        private static void ClearHoverAnchor(HazardInstance instance, GridCoord? coord = null)
        {
            if (instance == null) return;

            if (coord.HasValue)
            {
                if (instance.HoverCells.TryGetValue(coord.Value, out var cell))
                {
                    DestroyVfx(cell);
                    instance.HoverCells.Remove(coord.Value);
                }
                return;
            }

            DestroyVfx(instance.HoverAnchor);
            instance.HoverAnchor = null;
            instance.HoverCells.Clear();
        }

        /// <summary>Apaga una casilla, o todas con <paramref name="coord"/> en <c>null</c>.</summary>
        private static void ClearPersistentVfx(HazardInstance instance, GridCoord? coord = null)
        {
            if (instance == null || instance.PersistentVfx.Count == 0) return;

            if (coord.HasValue)
            {
                if (instance.PersistentVfx.TryGetValue(coord.Value, out var one))
                {
                    DestroyVfx(one);
                    instance.PersistentVfx.Remove(coord.Value);
                }
                return;
            }

            foreach (var go in instance.PersistentVfx.Values) DestroyVfx(go);
            instance.PersistentVfx.Clear();
        }

        /// <summary>Fuera de play mode <c>Destroy</c> es diferido y no llega a aplicarse en un test.</summary>
        private static void DestroyVfx(GameObject go)
        {
            if (go == null) return;

            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>No-op sin <see cref="HazardDefinitionSO.TriggerVfxPrefab"/>.</summary>
        private static void SpawnTriggerVfx(HazardDefinitionSO definition, GridCoord coord)
        {
            var prefab = definition?.TriggerVfxPrefab;
            if (prefab == null) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[HazardService] IGridManager no registrado — el hazard cobra igual, " +
                                 "pero sin VFX (no hay con qué ubicar la tile en el mundo).");
                return;
            }

            var world = grid.GridToWorld(coord) + Vector3.up * definition.TriggerVfxYOffset;
            var instance = Object.Instantiate(prefab, world, Quaternion.identity);
            instance.name = $"{prefab.name} (hazard)";

            // El destroy diferido es no-op (y loguea) fuera de play mode: sólo se arma jugando.
            if (Application.isPlaying && definition.TriggerVfxLifetime > 0f)
                Object.Destroy(instance, definition.TriggerVfxLifetime);
        }

        /// <summary>
        /// Whether <paramref name="entityGuid"/> is someone this hazard may bill. Guards damage, the
        /// trigger event <i>and</i> tile consumption together: a boss that set off its own ice
        /// without paying would still be burning the trap for the player. Fail-closed on a missing
        /// <see cref="IPlayerService"/> — a hazard that cannot name the player bills nobody.
        /// </summary>
        private bool ShouldBill(HazardInstance instance, Guid entityGuid)
        {
            if (instance.Definition.Affects == HazardAffects.Everyone) return true;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null)
            {
                WarnMissingPlayerServiceOnce();
                return false;
            }

            return entityGuid == playerService.PlayerGuid;
        }

        /// <summary>Once per service instance: the miss recurs on every turn end of every entity.</summary>
        private void WarnMissingPlayerServiceOnce()
        {
            if (_warnedMissingPlayerService) return;
            _warnedMissingPlayerService = true;

            Debug.LogWarning("[HazardService] IPlayerService no registrado — los hazards PlayerOnly " +
                             "no cobran a nadie. Revisá el orden de registro en ServiceBootstrap.");
        }

        /// <summary>One hazard hit: damage (if any), the trigger event, and tile consumption.</summary>
        private void TriggerInstance(HazardInstance instance, Guid entityGuid, GridCoord coord)
        {
            ApplyHazardDamage(instance, entityGuid);

            // Antes del evento: un listener puede expirar la instancia, y el golpe ya ocurrió.
            SpawnTriggerVfx(instance.Definition, coord);

            EventManager.Trigger(EventName.OnHazardTriggered, instance.InstanceId, entityGuid);

            if (!instance.Definition.ConsumeOnTrigger) return;
            if (!instance.Tiles.Remove(coord)) return;

            // La casilla gastada apaga su llama y su hover: si no, seguiría ardiendo (y
            // hablando) una casilla que ya no cobra.
            ClearPersistentVfx(instance, coord);
            ClearHoverAnchor(instance, coord);

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

            // Cheapest recurring retry: IMovementService may register after the hazard, or be
            // swapped out between runs.
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
            if (!grid.TryGetPosition(entityGuid, out _)) return;

            // Fase C: un footprint multi-celda arde si CUALQUIER celda cubierta pisa el hazard
            // — y cobra UNA vez por instancia (la primera celda cubierta que matchea).
            var covered = new List<GridCoord>(grid.OccupiedCells(entityGuid));

            foreach (var instance in new List<HazardInstance>(_instances.Values))
            {
                // The snapshot can outlive an instance: an earlier iteration may have expired it.
                if (!_instances.ContainsKey(instance.InstanceId)) continue;
                if (instance.Definition == null) continue;
                if (instance.Definition.Trigger != HazardTriggerMode.OnTurnEndInTile) continue;
                if (!TryFirstCovered(instance, covered, out var coord)) continue;
                if (!ShouldBill(instance, entityGuid)) continue;

                if (instance.SkipNextTick)
                {
                    // Consumed only by a tick that would have landed, so a boss can arm it during
                    // its own turn without knowing yet who is standing in the fire.
                    instance.SkipNextTick = false;
                    continue;
                }

                TriggerInstance(instance, entityGuid, coord);
            }
        }

        /// <summary>Primera celda cubierta por la entidad que cae dentro del hazard.</summary>
        private static bool TryFirstCovered(HazardInstance instance, List<GridCoord> covered, out GridCoord hit)
        {
            for (int i = 0; i < covered.Count; i++)
            {
                if (instance.Tiles.Contains(covered[i]))
                {
                    hit = covered[i];
                    return true;
                }
            }
            hit = default;
            return false;
        }

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (entity == Guid.Empty || _instances.Count == 0) return;

            // Fase C: el path que llega son ANCLAS; cada paso se expande a las celdas que el
            // footprint cubre en ese paso, y una instancia dispara a lo sumo UNA vez por paso
            // (dos celdas del mismo fuego bajo el rect = un cobro). Para 1×1 el paso cubre una
            // sola celda y el comportamiento es el de siempre (sí re-dispara en pasos sucesivos).
            var footprint = ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null
                ? grid.GetFootprint(entity)
                : GridFootprint.Unit;

            foreach (var stepAnchor in EnteredTiles(from, to, path))
            {
                var firedThisStep = new HashSet<HazardInstance>();
                foreach (var coord in GridFootprint.Cells(stepAnchor, footprint))
                {
                    // Re-snapshot per step: a trigger can consume tiles or expire an instance mid-scan.
                    foreach (var instance in new List<HazardInstance>(_instances.Values))
                    {
                        // An earlier step of this same path may have consumed the instance's last tile.
                        if (!_instances.ContainsKey(instance.InstanceId)) continue;
                        if (instance.Definition == null) continue;
                        if (instance.Definition.Trigger != HazardTriggerMode.OnEnter) continue;
                        if (!instance.Tiles.Contains(coord)) continue;
                        if (firedThisStep.Contains(instance)) continue;
                        if (!ShouldBill(instance, entity)) continue;

                        firedThisStep.Add(instance);
                        TriggerInstance(instance, entity, coord);
                    }
                }
            }
        }

        /// <summary>The path minus the origin, so walking through a trap triggers it.</summary>
        private static IEnumerable<GridCoord> EnteredTiles(GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (path == null || path.Count == 0)
            {
                // Teleports report no path, but the destination is still an entry.
                if (!to.Equals(from)) yield return to;
                yield break;
            }

            // IMovementService paths include the origin. Dropped by value, not by index, so this
            // holds whichever end it is serialized at.
            foreach (var coord in path)
            {
                if (!coord.Equals(from)) yield return coord;
            }
        }

        // ======================================================================
        // Instance state
        // ======================================================================

        private sealed class HazardInstance
        {
            public Guid InstanceId;
            public HazardDefinitionSO Definition;
            public HashSet<GridCoord> Tiles;

            public Guid OwnerGuid;

            /// <summary>Indexado por casilla para poder apagar sólo la que se consume.</summary>
            public readonly Dictionary<GridCoord, GameObject> PersistentVfx =
                new Dictionary<GridCoord, GameObject>();

            /// <summary>
            /// Raíz del hover de la instancia (tooltip + trigger) con un collider hijo por
            /// casilla. Hijos indexados para poder soltar sólo la casilla que se consume.
            /// </summary>
            public GameObject HoverAnchor;

            public readonly Dictionary<GridCoord, GameObject> HoverCells =
                new Dictionary<GridCoord, GameObject>();

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
