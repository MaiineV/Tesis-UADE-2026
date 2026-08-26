using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using Rollgeon.Tiles.Effects;
using Rollgeon.Tiles.Forced;
using UnityEngine;
// Sin el alias, `Object` resolvería a System.Object y los helpers de escena no compilarían.
using Object = UnityEngine.Object;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Registry + lifecycle + motor de triggers de las Casillas Especiales (GDD). Convive
    /// con <c>HazardService</c> (hazards de bosses) sin tocarlo; copia sus semánticas
    /// probadas: <see cref="EnteredTiles"/> descarta el origen por valor, snapshots por paso,
    /// tick de duración en el wrap de ronda, y teardown silencioso (sin eventos de expiry).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Filtros por disparo, en orden fijo:</b> trigger mask → celda armada → affinity
    /// (Ground/Flying) → ownership (jefes inmunes a casillas de owner jefe) → Zona de
    /// Seguridad → handler de la categoría.
    /// </para>
    /// <para>
    /// <b>Duraciones:</b> tickean solo cuando <c>OnTurnQueueBuilt</c> trae
    /// <c>roundIndex == último + 1</c> — el <c>Append</c> de refuerzos re-dispara el evento
    /// con el MISMO round y un tick naive descontaría vida de más; un resume salta varios
    /// rounds y solo sincroniza.
    /// </para>
    /// </remarks>
    public sealed class SpecialTileService : ISpecialTileService, IKillCreditResolver,
        IMovementPathFilter, IOutgoingFlatDamageBonusProvider, ISpecialTileAIQuery,
        IPreloadableService, IDisposable
    {
        /// <summary>Tope duro de la worklist de cadenas: dos portales enfrentados o un anillo
        /// de hielo no pueden colgar el juego.</summary>
        private const int ChainBudget = 64;

        private readonly Dictionary<Guid, SpecialTileInstance> _instances =
            new Dictionary<Guid, SpecialTileInstance>();

        // Sobrevive a la expiración de las instancias: el veneno de una casilla ya expirada
        // sigue matando con su instanceId como SourceId, y el crédito sigue siendo del player.
        private readonly HashSet<Guid> _everCreatedInstanceIds = new HashSet<Guid>();

        private readonly Dictionary<TileEffectCategory, ITileEffectHandler> _handlers =
            new Dictionary<TileEffectCategory, ITileEffectHandler>();

        // Estado por servicio, no estático: dos servicios (producción + un fixture) no pueden
        // repartirse los mismos clones. El pool no toca la escena hasta el primer alquiler, así
        // que los tests que no autoran VisualPrefab siguen sin parir GameObjects.
        private readonly Visuals.SpecialTileVisualPool _visualPool = new Visuals.SpecialTileVisualPool();

        private EventManager.EventReceiver _onTurnQueueBuiltHandler;
        private EventManager.EventReceiver _onTurnStartedHandler;
        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;
        private EventManager.EventReceiver _onRoomEnteredHandler;

        // La instancia exacta a la que nos suscribimos: es run-scoped y este servicio es
        // Global — re-resolver al desuscribir podría devolver una más nueva y leakear la vieja.
        private IMovementService _movementSubscribedTo;

        private int _lastSeenRoundIndex;
        private Func<Guid> _playerGuidResolver;

        // Guard de reentrada: los CommitPath que emite el propio motor de cadenas disparan
        // OnEntityMoved, y sin esto el handler los procesaría de nuevo (efectos dobles).
        // HazardService y los visuals sí los reciben — deben verlos como movimientos normales.
        private bool _resolvingChain;

        /// <summary>Entre Movement (78) y Threat/AI (80): usa movimiento, lo consumen hazte/IA.</summary>
        public int Priority => 79;

        public SpecialTileService()
        {
            RegisterEffectHandler(new DamageTileEffect());
            RegisterEffectHandler(new HealTileEffect());
            RegisterEffectHandler(new ApplyStatusTileEffect());
        }

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _playerGuidResolver = DefaultPlayerGuidResolver;
            SubscribeHandlers();

            ServiceLocator.AddService<ISpecialTileService>(this, ServiceScope.Global);
            ServiceLocator.AddService<SpecialTileService>(this, ServiceScope.Global);
            ServiceLocator.AddService<IKillCreditResolver>(this, ServiceScope.Global);
            ServiceLocator.AddService<IOutgoingFlatDamageBonusProvider>(this, ServiceScope.Global);
            ServiceLocator.AddService<ISpecialTileAIQuery>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: resolver de player + suscripciones armadas.</summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;
            SubscribeHandlers();
        }

        /// <summary>Registra (o pisa) el handler de una categoría. Extensión sin refactor.</summary>
        public void RegisterEffectHandler(ITileEffectHandler handler)
        {
            if (handler == null) return;
            _handlers[handler.Category] = handler;
        }

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();

            _onTurnQueueBuiltHandler = OnTurnQueueBuiltExternal;
            _onTurnStartedHandler = OnTurnStartedExternal;
            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onCombatEndHandler = OnCombatEndExternal;
            _onRunEndHandler = OnHardResetExternal;
            _onRoomEnteredHandler = OnHardResetExternal;

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
        }

        private void UnsubscribeHandlers()
        {
            void Unsub(EventName name, ref EventManager.EventReceiver handler)
            {
                if (handler == null) return;
                EventManager.UnSubscribe(name, handler);
                handler = null;
            }

            Unsub(EventName.OnTurnQueueBuilt, ref _onTurnQueueBuiltHandler);
            Unsub(EventName.OnTurnStarted, ref _onTurnStartedHandler);
            Unsub(EventName.OnTurnFinished, ref _onTurnFinishedHandler);
            Unsub(EventName.OnCombatEnd, ref _onCombatEndHandler);
            Unsub(EventName.OnRunEnd, ref _onRunEndHandler);
            Unsub(EventName.OnRoomEntered, ref _onRoomEnteredHandler);
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            ResetAll();
            _everCreatedInstanceIds.Clear();

            // El UNICO destroy real de visuales. En producción nadie llama a Dispose
            // (SpecialTileServiceBootstrap no lo hace), así que jugando los clones viven
            // estacionados y acotados por el tope del pool; esto es para tests y shutdown.
            _visualPool.Dispose();
        }

        // ======================================================================
        // ISpecialTileService — colocación
        // ======================================================================

        /// <inheritdoc />
        public Guid Place(SpecialTileDefinitionSO definition, IEnumerable<GridCoord> coords,
            TilePlacementOptions options = default)
        {
            if (definition == null || coords == null) return Guid.Empty;

            var set = new HashSet<GridCoord>(coords);
            if (set.Count == 0) return Guid.Empty;

            var instance = new SpecialTileInstance
            {
                InstanceId = Guid.NewGuid(),
                Definition = definition,
                Tiles = set,
                OwnerGuid = options.Owner,
                RemainingRounds = options.DurationRounds > 0
                    ? options.DurationRounds
                    : Mathf.Max(0, definition.DefaultDurationRounds),
            };

            if (definition.DisarmOnTrigger)
            {
                instance.CellArmed = new Dictionary<GridCoord, bool>();
                foreach (var coord in set) instance.CellArmed[coord] = true;
            }

            _instances[instance.InstanceId] = instance;
            _everCreatedInstanceIds.Add(instance.InstanceId);

            if (options.LinkTo != Guid.Empty && _instances.TryGetValue(options.LinkTo, out var pair))
            {
                instance.LinkedInstanceId = pair.InstanceId;
                pair.LinkedInstanceId = instance.InstanceId;
            }

            EnsureMovementSubscription();
            SpawnVisuals(instance);
            EventManager.Trigger(EventName.OnSpecialTilePlaced, instance.InstanceId);
            return instance.InstanceId;
        }

        /// <inheritdoc />
        public Guid CreateRuntime(SpecialTileDefinitionSO definition, GridCoord coord,
            RuntimeTileRequest request, out TilePlacementError error)
        {
            error = ValidateRuntimeCreation(definition, coord, request);
            if (error != TilePlacementError.None) return Guid.Empty;

            return Place(definition, new[] { coord }, new TilePlacementOptions
            {
                Owner = request.Owner,
                DurationRounds = request.Permanent ? 0 : request.DurationRounds,
                LinkTo = request.LinkTo,
            });
        }

        private TilePlacementError ValidateRuntimeCreation(
            SpecialTileDefinitionSO definition, GridCoord coord, in RuntimeTileRequest request)
        {
            if (definition == null) return TilePlacementError.NullDefinition;
            if (request.Owner == Guid.Empty) return TilePlacementError.MissingOwner;
            if (!request.Permanent && request.DurationRounds <= 0) return TilePlacementError.MissingDuration;

            // GDD: las Zonas de Seguridad Temporal/Móvil las crean SOLO jefes.
            if (definition.Category == TileEffectCategory.ConditionalProtection && !IsBossGuid(request.Owner))
                return TilePlacementError.OwnerNotAuthorized;

            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                if (!grid.IsWalkable(coord)) return TilePlacementError.CoordNotWalkable;
                if (grid.IsOccupied(coord)) return TilePlacementError.CoordOccupiedByUnit;
            }

            if (TryGetTileAt(coord, out _)) return TilePlacementError.CoordHasSpecialTile;

            return TilePlacementError.None;
        }

        // ======================================================================
        // ISpecialTileService — consultas y lifecycle
        // ======================================================================

        /// <inheritdoc />
        public bool TryGetTileAt(GridCoord coord, out SpecialTileInfo info)
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
        public IEnumerable<SpecialTileInfo> ActiveInstances()
        {
            // Materializado: los callers reaccionan mutando el dict a mitad de enumeración.
            var snapshot = new List<SpecialTileInfo>(_instances.Count);
            foreach (var instance in _instances.Values)
                snapshot.Add(instance.ToInfo());
            return snapshot;
        }

        /// <inheritdoc />
        public void Remove(Guid instanceId) => ExpireInstance(instanceId);

        /// <inheritdoc />
        public void MoveInstance(Guid instanceId, IEnumerable<GridCoord> newCoords)
        {
            if (newCoords == null) return;
            if (!_instances.TryGetValue(instanceId, out var instance)) return;

            var set = new HashSet<GridCoord>(newCoords);
            if (set.Count == 0) return;

            ClearVisuals(instance);
            instance.Tiles = set;

            if (instance.Definition.DisarmOnTrigger)
            {
                // Re-keyear el estado: celdas nuevas nacen armadas.
                instance.CellArmed = new Dictionary<GridCoord, bool>();
                foreach (var coord in set) instance.CellArmed[coord] = true;
            }

            SpawnVisuals(instance);
        }

        /// <inheritdoc />
        public bool HasAnySpecialTiles => _instances.Count > 0;

        // ======================================================================
        // IKillCreditResolver
        // ======================================================================

        /// <inheritdoc />
        public bool TryResolveCredit(Guid sourceId, out Guid credit)
        {
            if (sourceId != Guid.Empty && _everCreatedInstanceIds.Contains(sourceId))
            {
                var player = ResolvePlayerGuid();
                if (player != Guid.Empty)
                {
                    credit = player;
                    return true;
                }
            }
            credit = Guid.Empty;
            return false;
        }

        // ======================================================================
        // ISpecialTileAIQuery — vista contextualizada para el pathing IA
        // ======================================================================

        /// <inheritdoc />
        public bool AnyActiveDangerTelegraph
        {
            get
            {
                foreach (var instance in _instances.Values)
                {
                    var def = instance.Definition;
                    if (def == null || def.Category != TileEffectCategory.Telegraph) continue;
                    if (def.AIAnnouncesLethal) return true;
                    var payload = def.TelegraphPayload;
                    if (payload != null && (payload.EnterDamage > 0 || payload.TurnStartDamage > 0)) return true;
                }
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetTileFor(GridCoord coord, Guid entity, Cardinal entryDirection, out SpecialTileAIView view)
        {
            view = default;
            if (_instances.Count == 0) return false;

            int enter = 0, stay = 0, virtualDmg = 0, telegraphDmg = 0;
            var benefit = BeneficialTileKind.None;
            bool forced = false, portal = false, telegraph = false, lethal = false;
            GridCoord forcedDest = default;
            bool any = false;

            foreach (var instance in _instances.Values)
            {
                var def = instance.Definition;
                if (def == null || !instance.Tiles.Contains(coord)) continue;
                if (!ShouldAffect(instance, entity, coord)) continue;
                any = true;

                switch (def.Category)
                {
                    case TileEffectCategory.Damage:
                        enter += def.EnterDamage;
                        stay += def.TurnStartDamage;
                        virtualDmg += def.AIVirtualEnterDamage;
                        break;

                    case TileEffectCategory.ApplyStatus:
                        if (def.StatusKind == TileStatusKind.Poison)
                            enter += ExpectedPoisonDamage(entity, def);
                        virtualDmg += def.AIVirtualEnterDamage;
                        break;

                    case TileEffectCategory.Heal:
                        benefit = BeneficialTileKind.Healing;
                        break;

                    case TileEffectCategory.StatModifier:
                        benefit = BeneficialTileKind.Fortress;
                        break;

                    case TileEffectCategory.MoveRangeBonus:
                        benefit = BeneficialTileKind.Impulse;
                        break;

                    case TileEffectCategory.ConditionalProtection:
                        benefit = BeneficialTileKind.SafeZone;
                        break;

                    case TileEffectCategory.ForcedSlide:
                        forced = true;
                        portal = false;
                        forcedDest = ComputeSlideDestination(coord, entryDirection, entity);
                        break;

                    case TileEffectCategory.Teleport:
                        if (HasValidPortalExit(instance) && TryResolvePortalExit(instance, out var exit))
                        {
                            forced = true;
                            portal = true;
                            forcedDest = exit;
                        }
                        break;

                    case TileEffectCategory.Telegraph:
                        telegraph = true;
                        lethal |= def.AIAnnouncesLethal;
                        if (def.TelegraphPayload != null)
                            telegraphDmg += Mathf.Max(def.TelegraphPayload.EnterDamage,
                                def.TelegraphPayload.TurnStartDamage);
                        break;
                }
            }

            if (!any) return false;
            view = new SpecialTileAIView(enter, stay, virtualDmg, benefit,
                forced, portal, forcedDest, telegraph, telegraphDmg, lethal);
            return true;
        }

        /// <summary>GDD Veneno: 15 esperado si no está envenenada (5×3), 5 si ya lo está (refresh).</summary>
        private static int ExpectedPoisonDamage(Guid entity, SpecialTileDefinitionSO def)
        {
            if (ServiceLocator.TryGetService<Rollgeon.Combat.Status.IPoisonService>(out var poison)
                && poison != null && poison.IsPoisoned(entity))
            {
                return def.StatusTickDamage;
            }
            return def.StatusTickDamage * def.StatusTurns;
        }

        /// <summary>Fin del deslizamiento desde <paramref name="from"/> en <paramref name="dir"/> —
        /// read-only, espeja la física del motor de cadenas.</summary>
        private GridCoord ComputeSlideDestination(GridCoord from, Cardinal dir, Guid entity)
        {
            if (!TryGetGrid(out var grid)) return from;

            var current = from;
            for (int i = 0; i < ChainBudget; i++)
            {
                var next = dir.Step(current);
                if (!IsFreeCell(grid, next)) return current;
                current = next;
                if (!TryGetMovementTerminator(current, entity, out _, out bool isPortal) || isPortal)
                    return current;
            }
            return current;
        }

        // ======================================================================
        // IOutgoingFlatDamageBonusProvider — Fortaleza (OnRemainOn derivado)
        // ======================================================================

        /// <inheritdoc />
        public int GetFlatBonus(DamageContext ctx)
        {
            if (ctx == null || _instances.Count == 0) return 0;
            if (!TryGetGrid(out var grid) || !grid.TryGetPosition(ctx.SourceId, out var coord)) return 0;

            foreach (var instance in _instances.Values)
            {
                var def = instance.Definition;
                if (def == null || def.Category != TileEffectCategory.StatModifier) continue;
                if ((def.Triggers & TileTrigger.OnRemainOn) == 0) continue;
                if (!instance.Tiles.Contains(coord)) continue;
                if (!ShouldAffect(instance, ctx.SourceId, coord)) continue;

                // Una sola casilla física por celda (GDD §9): la unidad ocupa UNA celda,
                // así que las Fortalezas adyacentes no suman por construcción.
                return def.ComboDamageBonus;
            }
            return 0;
        }

        // ======================================================================
        // Motor de triggers
        // ======================================================================

        /// <inheritdoc />
        public void ResolveEntries(Guid entity, IReadOnlyList<GridCoord> enteredCoords, TileMovementKind kind)
        {
            if (entity == Guid.Empty || enteredCoords == null || _instances.Count == 0) return;
            if (kind == TileMovementKind.Teleport || kind == TileMovementKind.Spawn) return;

            var entryMask = EntryTriggerMask(kind);

            foreach (var coord in enteredCoords)
            {
                // Chequeo de muerte a mitad de cadena: CombatDeathWatcher desregistra del grid
                // sincrónicamente al golpe letal — un muerto no sigue pisando casillas.
                if (!TryGetGrid(out var grid) || !grid.TryGetPosition(entity, out _)) return;

                // Re-snapshot por paso: un trigger puede expirar instancias a mitad de scan.
                foreach (var instance in new List<SpecialTileInstance>(_instances.Values))
                {
                    if (!_instances.ContainsKey(instance.InstanceId)) continue;
                    if (instance.Definition == null) continue;
                    if ((instance.Definition.Triggers & entryMask) == 0) continue;
                    if (!instance.Tiles.Contains(coord)) continue;
                    if (!ShouldAffect(instance, entity, coord)) continue;

                    var fired = ResolveFiredTrigger(instance.Definition.Triggers, kind);
                    TriggerInstance(instance, entity, coord, fired, kind);
                }
            }
        }

        /// <summary>Triggers que satisface una entrada según cómo llegó la unidad.</summary>
        private static TileTrigger EntryTriggerMask(TileMovementKind kind)
        {
            const TileTrigger entry = TileTrigger.OnEnter | TileTrigger.OnPassThrough;
            return kind == TileMovementKind.Forced || kind == TileMovementKind.PortalRemainder
                ? entry | TileTrigger.OnForcedMovementInto
                : entry;
        }

        /// <summary>El trigger más específico que disparó, para el contexto del handler y el evento.</summary>
        private static TileTrigger ResolveFiredTrigger(TileTrigger defined, TileMovementKind kind)
        {
            bool forced = kind == TileMovementKind.Forced || kind == TileMovementKind.PortalRemainder;
            if (forced && (defined & TileTrigger.OnForcedMovementInto) != 0)
                return TileTrigger.OnForcedMovementInto;
            if ((defined & TileTrigger.OnEnter) != 0) return TileTrigger.OnEnter;
            return TileTrigger.OnPassThrough;
        }

        /// <summary>
        /// Dispara triggers de "estar parado" (OnTurnStart / OnEndTurn) sobre la casilla que
        /// la entidad ocupa en este instante.
        /// </summary>
        private void ResolveStand(Guid entity, TileTrigger trigger)
        {
            if (entity == Guid.Empty || _instances.Count == 0) return;
            if (!TryGetGrid(out var grid) || !grid.TryGetPosition(entity, out var coord)) return;

            foreach (var instance in new List<SpecialTileInstance>(_instances.Values))
            {
                if (!_instances.ContainsKey(instance.InstanceId)) continue;
                if (instance.Definition == null) continue;
                if ((instance.Definition.Triggers & trigger) == 0) continue;
                if (!instance.Tiles.Contains(coord)) continue;
                if (!ShouldAffect(instance, entity, coord)) continue;

                TriggerInstance(instance, entity, coord, trigger, TileMovementKind.Voluntary);
            }
        }

        /// <inheritdoc />
        public void CollectTypesUnder(Guid entity, List<SpecialTileType> into)
        {
            if (into == null) return;
            into.Clear();
            if (entity == Guid.Empty || _instances.Count == 0) return;
            if (!TryGetGrid(out var grid) || !grid.TryGetPosition(entity, out var coord)) return;

            foreach (var instance in _instances.Values)
            {
                var def = instance.Definition;
                if (def == null) continue;
                if (!instance.Tiles.Contains(coord)) continue;
                // Mismos filtros que un disparo real: un ícono de "quemándose" sobre un fuego
                // del que una Zona de Seguridad te protege sería mentirle al jugador.
                if (!ShouldAffect(instance, entity, coord)) continue;
                if (!into.Contains(def.TileType)) into.Add(def.TileType);
            }
        }

        /// <summary>
        /// Filtros transversales, en el orden del plan: celda armada → affinity → ownership
        /// → Zona de Seguridad. La máscara de trigger ya se chequeó en el caller.
        /// </summary>
        private bool ShouldAffect(SpecialTileInstance instance, Guid entity, GridCoord coord)
        {
            var def = instance.Definition;

            if (def.DisarmOnTrigger
                && instance.CellArmed != null
                && instance.CellArmed.TryGetValue(coord, out var armed)
                && !armed)
            {
                return false;
            }

            var traits = GetTraits(entity);
            if (def.Affinity == TileAffinity.GroundOnly && traits.IsFlying) return false;

            // GDD §15: los jefes no son afectados por casillas cuyo owner es un jefe (la
            // propia incluida). Owner no-jefe (player u otro enemigo) se quema con la suya.
            if (def.OwnerBossImmune && traits.IsBoss && IsBossGuid(instance.OwnerGuid)) return false;

            if (IsProtectedAt(coord, def.TileType, instance.InstanceId)) return false;

            return true;
        }

        /// <summary>
        /// Zona de Seguridad: ¿alguna instancia de protección activa cubre <paramref name="coord"/>
        /// y declara protección contra <paramref name="tileType"/>? Protege a CUALQUIER unidad
        /// adentro, incluidos enemigos y el owner.
        /// </summary>
        private bool IsProtectedAt(GridCoord coord, SpecialTileType tileType, Guid exceptInstanceId)
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.InstanceId == exceptInstanceId) continue;
                var def = instance.Definition;
                if (def == null || def.Category != TileEffectCategory.ConditionalProtection) continue;
                if (!instance.Tiles.Contains(coord)) continue;
                if (def.ProtectedTileTypes == null) continue;

                foreach (var protectedType in def.ProtectedTileTypes)
                    if (protectedType == tileType) return true;
            }
            return false;
        }

        /// <summary>
        /// Un disparo: handler de la categoría, desarme de la celda si corresponde, VFX y el
        /// evento con el que otros sistemas montan encima (el binder de estados vive afuera).
        /// </summary>
        private void TriggerInstance(SpecialTileInstance instance, Guid entity, GridCoord coord,
            TileTrigger fired, TileMovementKind kind)
        {
            var def = instance.Definition;

            if (_handlers.TryGetValue(def.Category, out var handler) && handler != null)
            {
                handler.Apply(new TileEffectContext(
                    instance.InstanceId, def, entity, coord, fired, kind, instance.OwnerGuid));
            }

            if (def.DisarmOnTrigger && instance.CellArmed != null)
            {
                instance.CellArmed[coord] = false;
                EventManager.Trigger(EventName.OnSpecialTileStateChanged,
                    instance.InstanceId, coord, false);
            }

            SpawnTriggerVfx(def, coord);
            EventManager.Trigger(EventName.OnSpecialTileTriggered,
                instance.InstanceId, entity, (int)fired);
        }

        // ======================================================================
        // Lifecycle interno
        // ======================================================================

        private void ExpireInstance(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return;
            if (!_instances.TryGetValue(instanceId, out var instance)) return;
            _instances.Remove(instanceId);

            if (instance.LinkedInstanceId != Guid.Empty
                && _instances.TryGetValue(instance.LinkedInstanceId, out var pair))
            {
                pair.LinkedInstanceId = Guid.Empty;
            }

            ClearVisuals(instance);
            EventManager.Trigger(EventName.OnSpecialTileExpired, instanceId);
        }

        private void ResetAll()
        {
            UnsubscribeMovement();

            // Sin OnSpecialTileExpired en teardown: los listeners no deben correr reacciones
            // de "la casilla se apagó" con el combate/la sala ya desmantelados.
            foreach (var instance in _instances.Values)
                ClearVisuals(instance);
            _instances.Clear();
            _lastSeenRoundIndex = 0;
        }

        private void TickDurations()
        {
            if (_instances.Count == 0) return;

            List<Guid> expired = null;
            foreach (var instance in _instances.Values)
            {
                if (instance.RemainingRounds <= 0) continue; // 0 = permanente.

                instance.RemainingRounds--;
                if (instance.RemainingRounds <= 0)
                    (expired ??= new List<Guid>()).Add(instance.InstanceId);
            }

            if (expired == null) return;
            foreach (var instanceId in expired)
            {
                // Telegraph: la marca se reemplaza por el resultado anunciado al vencer.
                // Capturar coords/owner ANTES de expirar — ExpireInstance borra la instancia.
                SpecialTileDefinitionSO payload = null;
                List<GridCoord> coords = null;
                Guid owner = Guid.Empty;
                if (_instances.TryGetValue(instanceId, out var instance)
                    && instance.Definition != null
                    && instance.Definition.Category == TileEffectCategory.Telegraph
                    && instance.Definition.TelegraphPayload != null)
                {
                    payload = instance.Definition.TelegraphPayload;
                    coords = new List<GridCoord>(instance.Tiles);
                    owner = instance.OwnerGuid;
                }

                ExpireInstance(instanceId);

                if (payload != null)
                    Place(payload, coords, new TilePlacementOptions { Owner = owner });
            }
        }

        private void RearmCells()
        {
            foreach (var instance in _instances.Values)
            {
                var def = instance.Definition;
                if (def == null || !def.RearmOnRoundWrap || instance.CellArmed == null) continue;

                // Snapshot de keys: se mutan valores dentro del loop.
                foreach (var coord in new List<GridCoord>(instance.CellArmed.Keys))
                {
                    if (instance.CellArmed[coord]) continue;
                    instance.CellArmed[coord] = true;
                    EventManager.Trigger(EventName.OnSpecialTileStateChanged,
                        instance.InstanceId, coord, true);
                }
            }
        }

        // ======================================================================
        // Suscripción a movimiento
        // ======================================================================

        private void EnsureMovementSubscription()
        {
            if (_movementSubscribedTo != null) return;
            if (_instances.Count == 0) return;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null) return;

            movement.OnEntityMoved += OnEntityMovedExternal;
            _movementSubscribedTo = movement;

            // El filtro trunca los movimientos voluntarios en la primera casilla terminadora
            // (Hielo/Portal) — el resto lo continúa el motor de cadenas.
            if (movement is IPathedMovementService pathed)
                pathed.SetPathFilter(this);
        }

        private void UnsubscribeMovement()
        {
            if (_movementSubscribedTo == null) return;
            _movementSubscribedTo.OnEntityMoved -= OnEntityMovedExternal;
            if (_movementSubscribedTo is IPathedMovementService pathed)
                pathed.SetPathFilter(null);
            _movementSubscribedTo = null;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is int roundIndex)) return;

            // Retry barato: IMovementService puede registrarse después que este servicio.
            EnsureMovementSubscription();

            // Solo el wrap real tickea (round == último + 1). BuildForCombat (0), el Append de
            // refuerzos (mismo round) y un resume (salto de varios) solo sincronizan.
            if (roundIndex == _lastSeenRoundIndex + 1)
            {
                _lastSeenRoundIndex = roundIndex;
                TickDurations();
                RearmCells();
            }
            else
            {
                _lastSeenRoundIndex = roundIndex;
            }
        }

        private void OnTurnStartedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid entity)) return;
            ResolveStand(entity, TileTrigger.OnTurnStart);
        }

        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid entity)) return;
            ResolveStand(entity, TileTrigger.OnEndTurn);
        }

        private void OnCombatEndExternal(params object[] args)
        {
            // Las temporales no sobreviven al combate (las rondas dejaron de existir); las
            // permanentes quedan — la sala sigue siendo transitable en exploración.
            List<Guid> temporary = null;
            foreach (var instance in _instances.Values)
            {
                if (instance.RemainingRounds > 0)
                    (temporary ??= new List<Guid>()).Add(instance.InstanceId);
            }
            if (temporary != null)
            {
                // Teardown silencioso: sin OnSpecialTileExpired (criterio de HazardService).
                foreach (var instanceId in temporary)
                {
                    if (!_instances.TryGetValue(instanceId, out var instance)) continue;
                    _instances.Remove(instanceId);
                    ClearVisuals(instance);
                }
            }

            _lastSeenRoundIndex = 0;
        }

        private void OnHardResetExternal(params object[] args) => ResetAll();

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (_resolvingChain) return; // segmentos emitidos por el propio motor de cadenas
            if (entity == Guid.Empty || _instances.Count == 0) return;

            var entered = new List<GridCoord>();
            foreach (var coord in EnteredTiles(from, to, path))
                entered.Add(coord);
            if (entered.Count == 0) return;

            ResolveEntries(entity, entered, TileMovementKind.Voluntary);

            // Continuación: si el movimiento terminó sobre Hielo/Portal (el filtro lo truncó
            // ahí), el motor sigue — deslizamiento o teleport + reubicación.
            var last = entered[entered.Count - 1];
            var prev = entered.Count >= 2 ? entered[entered.Count - 2] : from;
            var dir = CardinalExtensions.FromDelta(prev, last);
            RunChainCore(entity, dir, remainingForced: 0, TileMovementKind.Voluntary);
        }

        /// <summary>El path menos el origen — atravesar dispara igual que detenerse.</summary>
        private static IEnumerable<GridCoord> EnteredTiles(GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (path == null || path.Count == 0)
            {
                if (!to.Equals(from)) yield return to;
                yield break;
            }

            // Los paths de IMovementService incluyen el origen. Descartado por valor, no por
            // índice, así vale en cualquier extremo que esté serializado.
            foreach (var coord in path)
            {
                if (!coord.Equals(from)) yield return coord;
            }
        }

        // ======================================================================
        // IMovementPathFilter — truncado del movimiento voluntario
        // ======================================================================

        /// <inheritdoc />
        public IReadOnlyList<GridCoord> Filter(Guid entity, IReadOnlyList<GridCoord> plannedPath)
        {
            if (plannedPath == null || plannedPath.Count < 2 || _instances.Count == 0) return plannedPath;

            for (int i = 1; i < plannedPath.Count; i++)
            {
                if (!TryGetMovementTerminator(plannedPath[i], entity, out _, out _)) continue;

                var truncated = new List<GridCoord>(i + 1);
                for (int j = 0; j <= i; j++) truncated.Add(plannedPath[j]);
                return truncated;
            }
            return plannedPath;
        }

        // ======================================================================
        // Motor de cadenas — deslizamiento, portales, empuje (GDD §12)
        // ======================================================================

        /// <summary>
        /// Empuje: recorre <paramref name="steps"/> celdas hacia <paramref name="dir"/>,
        /// disparando los triggers de cada celda (kind Forced) y resolviendo continuaciones.
        /// Lo consume <see cref="Forced.ForcedMovementService"/>.
        /// </summary>
        public ForcedMoveResult RunForcedChain(Guid entity, Cardinal dir, int steps)
            => RunChainCore(entity, dir, steps, TileMovementKind.Forced);

        /// <summary>
        /// Worklist iterativa de movimiento encadenado. Orden por celda entrada (GDD §12):
        /// trigger de entrada → daño → estado → movimiento forzado adicional → chequeo de
        /// muerte. La celda ACTUAL decide la continuación: portal (teleport + paso/reubicación),
        /// hielo con empuje agotado (deslizar), o paso de empuje pendiente.
        /// </summary>
        private ForcedMoveResult RunChainCore(Guid entity, Cardinal dir, int remainingForced,
            TileMovementKind initialKind)
        {
            bool outermost = !_resolvingChain;
            _resolvingChain = true;
            try
            {
                int traveled = 0;
                var stop = ForcedMoveStop.CompletedDistance;
                var kind = initialKind;
                GridCoord blockedAt = default;
                Guid blockerGuid = Guid.Empty;

                if (!TryGetGrid(out var grid))
                    return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false);
                if (!TryGetPathedMovement(out var pathed))
                    return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false);

                // El choque se captura ANTES de cualquier reubicación (portal): el consumidor
                // (empuje del Guerrero) necesita saber contra QUÉ frenó, y FinalCoord puede
                // terminar fuera de la línea de empuje.
                void Block(GridCoord next)
                {
                    stop = ForcedMoveStop.Obstacle;
                    blockedAt = next;
                    if (!grid.TryGetOccupant(next, out blockerGuid)) blockerGuid = Guid.Empty;
                }

                int budget = ChainBudget;
                while (budget-- > 0)
                {
                    // Chequeo de muerte: CombatDeathWatcher desregistra del grid sincrónicamente.
                    if (!grid.TryGetPosition(entity, out var current)) { stop = ForcedMoveStop.Death; break; }

                    bool onTerminator = TryGetMovementTerminator(current, entity, out var terminator, out bool isPortal);

                    if (onTerminator && isPortal)
                    {
                        if (!TryResolvePortalExit(terminator, out var exit)
                            || !pathed.Teleport(entity, exit))
                        {
                            stop = ForcedMoveStop.PortalBlocked;
                            break;
                        }

                        // El cooldown se aplica ANTES de reubicar: si la celda de reubicación
                        // es otro portal, la próxima iteración ya lo ve como celda común y la
                        // cadena termina limpia (antes solo frenaba el ChainBudget).
                        ApplyTeleportCooldown(entity, terminator.Definition);

                        if (remainingForced > 0)
                        {
                            // El remanente del empuje sigue desde el otro portal; el primer
                            // paso desde la salida consume remanente (empuje 5, portal a 1 →
                            // 1 hasta el portal + 4 desde el otro lado).
                            kind = TileMovementKind.PortalRemainder;
                            var next = dir.Step(exit);
                            if (!IsFreeCell(grid, next))
                            {
                                // El empuje muere contra el borde, pero nadie queda parado
                                // sobre un portal: reubicación con fallback horario.
                                Block(next);
                                RelocateOffPortal(grid, pathed, entity, exit, ref dir, kind);
                                break;
                            }
                            if (!pathed.CommitPath(entity, new List<GridCoord> { exit, next })) { Block(next); break; }
                            remainingForced--;
                            traveled++;
                            ResolveEntries(entity, new[] { next }, kind);
                            continue;
                        }

                        // Movimiento voluntario: reubicación de 1 celda en la dirección de
                        // entrada, o la primera libre en sentido horario.
                        if (!RelocateOffPortal(grid, pathed, entity, exit, ref dir, kind))
                        {
                            Debug.LogWarning("[SpecialTileService] Portal sin vecina libre — la unidad queda sobre el portal.");
                            break;
                        }
                        continue;
                    }

                    if (onTerminator && remainingForced == 0)
                    {
                        // Hielo: la unidad sigue en la dirección de entrada hasta salir del
                        // hielo o chocar. Cada celda atravesada dispara sus propios triggers.
                        var next = dir.Step(current);
                        if (!IsFreeCell(grid, next)) { Block(next); break; }
                        if (!pathed.CommitPath(entity, new List<GridCoord> { current, next })) { Block(next); break; }
                        traveled++;
                        ResolveEntries(entity, new[] { next }, TileMovementKind.Slide);
                        continue;
                    }

                    if (remainingForced > 0)
                    {
                        // Paso de empuje normal. El hielo bajo un empuje activo no agrega
                        // nada: el empuje ya lo está arrastrando (si el empuje TERMINA sobre
                        // hielo, la próxima iteración desliza).
                        var next = dir.Step(current);
                        if (!IsFreeCell(grid, next)) { Block(next); break; }
                        if (!pathed.CommitPath(entity, new List<GridCoord> { current, next })) { Block(next); break; }
                        remainingForced--;
                        traveled++;
                        ResolveEntries(entity, new[] { next }, kind);
                        continue;
                    }

                    break; // celda común, sin empuje pendiente: la cadena terminó.
                }

                bool died = !grid.TryGetPosition(entity, out var finalCoord);
                if (died) stop = ForcedMoveStop.Death;
                return new ForcedMoveResult(finalCoord, traveled, stop, died, blockedAt, blockerGuid);
            }
            finally
            {
                if (outermost) _resolvingChain = false;
            }
        }

        /// <summary>
        /// Saca a la unidad del portal de salida: dirección de entrada primero, después la
        /// primera vecina libre en sentido horario. La celda de reubicación SÍ dispara
        /// OnEnter (puede haber especiales adyacentes al portal).
        /// </summary>
        private bool RelocateOffPortal(IGridManager grid, IPathedMovementService pathed,
            Guid entity, GridCoord exit, ref Cardinal dir, TileMovementKind kind)
        {
            var candidate = dir;
            for (int i = 0; i < 4; i++)
            {
                var reloc = candidate.Step(exit);
                if (IsFreeCell(grid, reloc))
                {
                    if (!pathed.CommitPath(entity, new List<GridCoord> { exit, reloc })) return false;
                    dir = candidate;
                    var entryKind = kind == TileMovementKind.Forced || kind == TileMovementKind.PortalRemainder
                        ? TileMovementKind.PortalRemainder
                        : TileMovementKind.Voluntary;
                    ResolveEntries(entity, new[] { reloc }, entryKind);
                    return true;
                }
                candidate = candidate.Clockwise();
            }
            return false;
        }

        /// <summary>
        /// Casilla que termina el movimiento en <paramref name="coord"/> para esta unidad:
        /// Hielo (ForcedSlide) o Portal (Teleport, solo con par linkeado — un portal huérfano
        /// es una celda común). Respeta los mismos filtros que cualquier trigger.
        /// </summary>
        private bool TryGetMovementTerminator(GridCoord coord, Guid entity,
            out SpecialTileInstance terminator, out bool isPortal)
        {
            terminator = null;
            isPortal = false;

            foreach (var instance in _instances.Values)
            {
                var def = instance.Definition;
                if (def == null) continue;

                bool portal = def.Category == TileEffectCategory.Teleport;
                bool ice = def.Category == TileEffectCategory.ForcedSlide;
                if (!portal && !ice) continue;
                if (!instance.Tiles.Contains(coord)) continue;
                if (portal && !HasValidPortalExit(instance)) continue;
                // En cooldown post-teleport el portal es una celda común: no trunca el path
                // ni teletransporta. Este es el único choke point — Filter, RunChainCore y
                // ComputeSlideDestination pasan todos por acá.
                if (portal && IsTeleportBlocked(entity)) continue;
                if (!ShouldAffect(instance, entity, coord)) continue;

                terminator = instance;
                isPortal = portal;
                return true;
            }
            return false;
        }

        private bool HasValidPortalExit(SpecialTileInstance portal)
            => portal.LinkedInstanceId != Guid.Empty && _instances.ContainsKey(portal.LinkedInstanceId);

        /// <summary>
        /// Defensivo a propósito: sin <c>ITeleportCooldownService</c> registrado los portales
        /// se comportan como siempre — los tests de cadenas existentes no necesitan el servicio.
        /// </summary>
        private static bool IsTeleportBlocked(Guid entity)
            => ServiceLocator.TryGetService<Rollgeon.Combat.Status.ITeleportCooldownService>(out var cooldown)
               && cooldown != null && cooldown.IsOnCooldown(entity);

        private static void ApplyTeleportCooldown(Guid entity, SpecialTileDefinitionSO def)
        {
            // Aplica en cualquier fase (playtest 23/08: los portales se usan sobre todo en
            // exploración). El reloj de expiración lo decide el servicio: turnos en combate,
            // movimientos en exploración.
            if (def == null || def.TeleportCooldownTurns <= 0) return;

            if (ServiceLocator.TryGetService<Rollgeon.Combat.Status.ITeleportCooldownService>(out var cooldown)
                && cooldown != null)
            {
                cooldown.Apply(entity, def.TeleportCooldownTurns);
            }
        }

        private bool TryResolvePortalExit(SpecialTileInstance portal, out GridCoord exit)
        {
            exit = default;
            if (!_instances.TryGetValue(portal.LinkedInstanceId, out var linked)) return false;
            foreach (var coord in linked.Tiles)
            {
                exit = coord;
                return true; // portales de 1 celda por diseño
            }
            return false;
        }

        private static bool IsFreeCell(IGridManager grid, GridCoord coord)
            => grid.IsWalkable(coord) && !grid.IsOccupied(coord);

        private bool TryGetPathedMovement(out IPathedMovementService pathed)
        {
            pathed = _movementSubscribedTo as IPathedMovementService;
            if (pathed != null) return true;
            if (ServiceLocator.TryGetService<IMovementService>(out var movement))
                pathed = movement as IPathedMovementService;
            return pathed != null;
        }

        // ======================================================================
        // Visuals (anchor de arte swapeable; overlay/binding llegan en la etapa H)
        // ======================================================================

        /// <summary>
        /// Anchor de arte: alquila un <see cref="SpecialTileDefinitionSO.VisualPrefab"/> por
        /// celda y bindea sus componentes opcionales (binding de eventos, tooltip). Sin
        /// prefab, cae al overlay de quads tintados — la casilla nunca queda invisible.
        /// </summary>
        /// <remarks>
        /// Pasa por <see cref="Visuals.SpecialTileVisualPool"/>: un ataque de jefe enciende
        /// ~112 casillas en un frame y las bandas repiten la ignición cada dos rondas — de la
        /// segunda en adelante no se instancia nada.
        /// </remarks>
        private void SpawnVisuals(SpecialTileInstance instance)
        {
            var prefab = instance?.Definition?.VisualPrefab;
            if (prefab == null)
            {
                ShowOverlayFallback(instance);
                return;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;

            foreach (var coord in instance.Tiles)
            {
                if (instance.Visuals.ContainsKey(coord)) continue;

                var world = grid.GridToWorld(coord) + Vector3.up * instance.Definition.VisualYOffset;
                var visual = _visualPool.Rent(prefab, world);
                if (visual == null) continue;

                visual.Go.name = $"{prefab.name} (tile {coord})";
                instance.Visuals[coord] = visual;

                // Re-bindeo en CADA alquiler, no al crear el clon: uno reciclado viene atado a
                // la instancia anterior y contestaría por una casilla que ya se apagó.
                if (visual.Binding != null) visual.Binding.Bind(instance.InstanceId, coord);
                if (visual.Tooltip != null) visual.Tooltip.Bind(instance.Definition, instance.RemainingRounds);
            }
        }

        /// <summary>Placeholder sin arte: quads tintados del overlay de amenazas. Solo en
        /// play mode — los tests EditMode no deben parir GameObjects de overlay.</summary>
        private static void ShowOverlayFallback(SpecialTileInstance instance)
        {
            if (!Application.isPlaying) return;
            if (instance == null || instance.Tiles.Count == 0) return;

            Rollgeon.Combat.Threat.ThreatTelegraphOverlay.ResolveOrCreate()
                .Show(instance.InstanceId, instance.Tiles, instance.Definition.OverlayTint);
        }

        /// <summary>TryGet, no ResolveOrCreate: limpiar jamás debe crear el overlay.</summary>
        private static void ClearOverlay(Guid key)
        {
            if (ServiceLocator.TryGetService<Rollgeon.Combat.Threat.IThreatOverlayService>(out var overlay)
                && overlay != null)
            {
                overlay.Clear(key);
            }
        }

        /// <summary>
        /// Apaga una casilla, o todas con <paramref name="coord"/> en <c>null</c>. Los clones se
        /// ESTACIONAN en el pool en vez de destruirse: la casilla que expira ahora es la misma
        /// que se va a volver a encender dos rondas después.
        /// </summary>
        private void ClearVisuals(SpecialTileInstance instance, GridCoord? coord = null)
        {
            if (instance == null) return;
            if (!coord.HasValue) ClearOverlay(instance.InstanceId);
            if (instance.Visuals.Count == 0) return;

            if (coord.HasValue)
            {
                if (instance.Visuals.TryGetValue(coord.Value, out var one))
                {
                    _visualPool.Park(one);
                    instance.Visuals.Remove(coord.Value);
                }
                return;
            }

            foreach (var visual in instance.Visuals.Values) _visualPool.Park(visual);
            instance.Visuals.Clear();
        }

        /// <summary>No-op sin <see cref="SpecialTileDefinitionSO.TriggerVfxPrefab"/>.</summary>
        private static void SpawnTriggerVfx(SpecialTileDefinitionSO definition, GridCoord coord)
        {
            var prefab = definition?.TriggerVfxPrefab;
            if (prefab == null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;

            var world = grid.GridToWorld(coord) + Vector3.up * definition.TriggerVfxYOffset;
            var vfx = Object.Instantiate(prefab, world, Quaternion.identity);
            vfx.name = $"{prefab.name} (tile vfx)";

            if (Application.isPlaying && definition.TriggerVfxLifetime > 0f)
                Object.Destroy(vfx, definition.TriggerVfxLifetime);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static bool TryGetGrid(out IGridManager grid)
            => ServiceLocator.TryGetService<IGridManager>(out grid) && grid != null;

        private static UnitTraits GetTraits(Guid entity)
        {
            if (ServiceLocator.TryGetService<IUnitTraitService>(out var traits) && traits != null)
                return traits.Get(entity);
            return UnitTraits.DefaultGround;
        }

        private static bool IsBossGuid(Guid entity)
            => entity != Guid.Empty && GetTraits(entity).IsBoss;

        private Guid ResolvePlayerGuid()
            => _playerGuidResolver != null ? _playerGuidResolver() : DefaultPlayerGuidResolver();

        private static Guid DefaultPlayerGuidResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var svc) && svc != null)
                return svc.PlayerGuid;
            return Guid.Empty;
        }

        // ======================================================================
        // Instance state
        // ======================================================================

        private sealed class SpecialTileInstance
        {
            public Guid InstanceId;
            public SpecialTileDefinitionSO Definition;
            public HashSet<GridCoord> Tiles;
            public Guid OwnerGuid;

            /// <summary>Rondas de vida; 0 = permanente.</summary>
            public int RemainingRounds;

            /// <summary>Instancia par (portales). Guid.Empty sin link.</summary>
            public Guid LinkedInstanceId;

            /// <summary>Solo con DisarmOnTrigger: armado por celda (Pinchos).</summary>
            public Dictionary<GridCoord, bool> CellArmed;

            /// <summary>Visual ALQUILADO por celda (anchor de arte). Se guarda el entry y no el
            /// GameObject: para devolverlo hay que saber de qué balde del pool salió.</summary>
            public readonly Dictionary<GridCoord, Visuals.PooledTileVisual> Visuals =
                new Dictionary<GridCoord, Visuals.PooledTileVisual>();

            /// <summary>Copia las coords — el snapshot promete inmutabilidad.</summary>
            public SpecialTileInfo ToInfo()
                => new SpecialTileInfo(InstanceId, Definition, new List<GridCoord>(Tiles),
                    RemainingRounds, OwnerGuid, LinkedInstanceId);
        }
    }
}
