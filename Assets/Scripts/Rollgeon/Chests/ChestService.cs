using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.State;
using Rollgeon.Economy;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Núcleo de la mecánica de Cofre (Feature#0046, GDD del Cofre). Spawn
    /// determinista al empezar el combate, resolución por origen del golpe final
    /// (jugador abre / otro rompe), expiración al terminar el combate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hook de spawn.</b> <c>OnCombatStart</c> (no <c>OnRoomEntered</c>): para
    /// entonces <c>RoomGridLoader</c> ya cargó la grilla y el resolver ya colocó a
    /// los combatientes, así el pick de tile libre ve la sala real. El cofre existe
    /// solo durante el combate (GDD §26), así que el hook además es semánticamente
    /// el correcto.
    /// </para>
    /// <para>
    /// <b>Determinismo.</b> Los rolls (spawn → tier → mimic) salen de
    /// <see cref="ChestSeed.Derive"/> sobre (floorSeed, celda) en orden fijo y se
    /// persisten en <see cref="ChestState"/>: re-entry y resume hidratan en vez de
    /// re-rollear. <c>Consumed</c> ⇒ nunca respawnea.
    /// </para>
    /// <para>
    /// <b>El cofre es una entidad sin turno.</b> Guid + Health en
    /// <see cref="AttributesManager"/> + tile en <see cref="IGridManager"/> + pawn:
    /// hereda gratis targeting del jugador, pipeline de daño y HP bar. NUNCA entra
    /// al turn order ni a <c>RoomInstance.SpawnedEnemies</c> (no bloquea el clear);
    /// por eso este service es quien lo limpia en <c>OnCombatEnd</c>.
    /// </para>
    /// </remarks>
    public sealed class ChestService : IChestService, Combat.Pipelines.IMinHpClampProvider, IDisposable
    {
        private const string LogPrefix = "[ChestService] ";
        private const string StateKey = "chest_0";

        // Trigger declarado por AnimCon_ChestMimic: transición Idle (cofre cerrado
        // misterioso) → Awaken → Idle Awaken. No-op para visuales sin el param.
        private const string MimicAwakenTrigger = "Awaken";

        private readonly ChestConfigSO _config;
        private readonly ChestLootPoolSO _lootPool;

        private ChestRuntime _active;
        private System.Random _rng;

        private EventManager.EventReceiver _onCombatStart;
        private EventManager.EventReceiver _onCombatEnd;
        private Action<DamageResolvedPayload> _onDamageResolved;

        public ChestRuntime ActiveChest => _active;

        public ChestService(ChestConfigSO config, ChestLootPoolSO lootPool)
        {
            _config = config;
            _lootPool = lootPool;

            _onCombatStart = OnCombatStart;
            _onCombatEnd = OnCombatEnd;
            _onDamageResolved = OnDamageResolved;
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStart);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
        }

        public void Dispose()
        {
            if (_onCombatStart != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatStart, _onCombatStart);
                _onCombatStart = null;
            }
            if (_onCombatEnd != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
                _onCombatEnd = null;
            }
            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            _active = null;
        }

        // -----------------------------------------------------------------
        // IChestRegistry
        // -----------------------------------------------------------------

        public bool IsChest(Guid guid) =>
            _active != null && _active.Phase == ChestPhase.Idle && _active.Guid == guid;

        public bool TryGetActiveChest(out Guid chestGuid)
        {
            if (_active != null && _active.Phase == ChestPhase.Idle)
            {
                chestGuid = _active.Guid;
                return true;
            }
            chestGuid = Guid.Empty;
            return false;
        }

        // -----------------------------------------------------------------
        // IMinHpClampProvider — clamp del Mimic (GDD §25)
        // -----------------------------------------------------------------

        /// <summary>
        /// Daño de enemigos/eventos nunca deja a un Mimic sin activar por debajo de
        /// <c>MimicClampHP</c> (1). No aplica a cofres normales ni a golpes del
        /// jugador — esos activan al Mimic directamente, sin importar el HP.
        /// </summary>
        public bool TryGetMinHp(Guid targetId, Guid sourceId, out int minHp)
        {
            minHp = _config != null ? _config.MimicClampHP : 1;
            return _active != null
                   && _active.Phase == ChestPhase.Idle
                   && _active.IsMimic
                   && _active.Guid == targetId
                   && !IsPlayer(sourceId);
        }

        // -----------------------------------------------------------------
        // Spawn
        // -----------------------------------------------------------------

        private void OnCombatStart(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Guid roomId) return;
            if (_active != null) return; // ya hay cofre activo (no debería pasar)
            if (_config == null || _lootPool == null) return;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return;
            if (room.Template == null || room.Template.Type != RoomType.Combat) return;

            // Estado persistido: hidratar o rollear una única vez por sala.
            if (!room.ObjectStates.TryGet<ChestState>(StateKey, out var state))
            {
                state = new ChestState { SpawnPointId = StateKey };
                RollChest(state, dungeon.CurrentFloorSeed, room.GridCell);
                room.ObjectStates.Set(StateKey, state);
            }

            if (state.Consumed || !state.Spawned) return;

            // El rng por-sala también alimenta el roll de loot al abrir.
            _rng = new System.Random(ChestSeed.Derive(dungeon.CurrentFloorSeed, room.GridCell));
            SpawnChest(room.InstanceId, (ItemRarity)state.TierIndex, state.IsMimic, _rng);
        }

        private void RollChest(ChestState state, int floorSeed, Vector2Int cell)
        {
            var rng = new System.Random(ChestSeed.Derive(floorSeed, cell));

            // Orden fijo de sub-rolls — cambiarlo rompe el determinismo de saves.
            state.Spawned = rng.NextDouble() < _config.SpawnFrequency;
            int floorNumber = CurrentFloorNumber;
            state.TierIndex = (int)_config.RollTier(rng, floorNumber);
            state.IsMimic = rng.NextDouble() < _config.MimicSpawnChance;
        }

        public bool DebugSpawn(ItemRarity tier, bool isMimic)
        {
            if (_config == null || _lootPool == null) return false;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return false;
            var room = dungeon.CurrentRoomInstance;
            if (room == null) return false;

            if (_active != null) DespawnEntity(_active.Guid);
            _active = null;

            _rng ??= new System.Random();
            return SpawnChest(room.InstanceId, tier, isMimic, _rng);
        }

        private bool SpawnChest(Guid roomInstanceId, ItemRarity tier, bool isMimic, System.Random rng)
        {
            var tierDef = _config.GetTierDef(tier);
            if (tierDef == null)
            {
                Debug.LogError(LogPrefix + $"Sin ChestTierDef para tier {tier} — no se spawnea.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            var coord = TryPickFreeCoord(grid, rng);
            if (coord == null)
            {
                Debug.LogWarning(LogPrefix + "Sin tiles libres para el cofre — se saltea el spawn.");
                return false;
            }

            var guid = Guid.NewGuid();

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return false;
            var chestAttrs = new ModifiableAttributes();
            chestAttrs.EnsureInitialized();
            chestAttrs.SetAttribute<Health>(new Health(tierDef.MaxHP));
            attrs.Register(guid, chestAttrs);

            grid.Register(guid, coord.Value);

            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var pawn = visuals.SpawnProp(guid, ChestVisuals.ResolvePrefab(_config, tierDef), coord.Value);
                if (pawn != null)
                {
                    ChestVisuals.Apply(pawn.gameObject, tierDef, _config.FittingsColor);
                    if (pawn.HealthBar != null)
                        pawn.HealthBar.Initialize(guid, tierDef.MaxHP, tierDef.MaxHP);

                    // GDD §18: solo los mimics dormidos twitchean — el hint es la
                    // única forma de olfatearlos antes del primer golpe.
                    if (isMimic)
                    {
                        pawn.gameObject.AddComponent<ChestMimicHint>().Init(
                            _config.MimicHintMinSeconds,
                            _config.MimicHintMaxSeconds,
                            _config.MimicHintDuration,
                            _config.MimicHintAngleDegrees);
                    }

                    // A prueba de mímico por diseño: el hover va en ESTE camino, que es el mismo
                    // para el cofre real y el disfrazado — mismo componente, mismo contenido.
                    AttachHoverTooltip(pawn.gameObject, tier);
                }
            }

            _active = new ChestRuntime
            {
                Guid = guid,
                RoomInstanceId = roomInstanceId,
                Tier = tier,
                IsMimic = isMimic,
                Coord = coord.Value,
            };

            EventManager.Trigger(EventName.OnChestSpawned, guid, (int)tier, isMimic);
            return true;
        }

        /// <summary>
        /// El hover del cofre: collider trigger (el prefab no trae ninguno) + el panel con
        /// nombre, rareza y una línea. Trigger y no sólido, como casillas y bombas: el pick de
        /// celda intersecta el plano del piso y no lo molesta.
        /// </summary>
        private static void AttachHoverTooltip(GameObject go, ItemRarity tier)
        {
            var info = go.GetComponent<ChestTooltipInfo>();
            if (info == null) info = go.AddComponent<ChestTooltipInfo>();
            info.Bind(tier);

            if (go.GetComponentInChildren<Collider>(includeInactive: true) == null)
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(0.9f, 1f, 0.9f);
                collider.center = new Vector3(0f, 0.5f, 0f);
            }

            var trigger = go.GetComponent<Rollgeon.UI.Tooltips.WorldTooltipTrigger>();
            if (trigger == null) trigger = go.AddComponent<Rollgeon.UI.Tooltips.WorldTooltipTrigger>();
            trigger.Mode = Rollgeon.UI.Tooltips.WorldTooltipMode.Hover;
            trigger.Placement = Rollgeon.UI.Tooltips.TooltipPlacementMode.ScreenTopRight;
            trigger.ContentProvider = info.BuildContent;
        }

        /// <summary>
        /// Tile walkable, libre y que no sea anchor de puerta — mismo criterio que
        /// <c>DefaultEnemySpawnResolver.TryPickRandomSpawnCoord</c>.
        /// </summary>
        private static GridCoord? TryPickFreeCoord(IGridManager grid, System.Random rng)
        {
            if (grid.Graph == null) return null;

            var forbidden = CollectDoorCoords(grid);
            var candidates = new List<GridCoord>();
            foreach (var coord in grid.Graph.AllCoords())
            {
                if (forbidden.Contains(coord)) continue;
                if (grid.IsOccupied(coord)) continue;
                if (!grid.IsWalkable(coord)) continue;
                // BUG-069: un nodo "caminable" del NavGraph puede tener grado 0 (isla —
                // ver NavGraphBaker) y quedar atrapado entre pared y assets. Grafo vacío =
                // "sin restricciones" (NavGraph.IsEmpty), ahí el chequeo no aplica.
                if (!grid.Graph.IsEmpty && !HasAnyNeighbor(grid.Graph, coord)) continue;
                candidates.Add(coord);
            }

            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
        }

        private static bool HasAnyNeighbor(NavGraph graph, GridCoord coord)
        {
            foreach (var _ in graph.GetNeighbors(coord)) return true;
            return false;
        }

        private static HashSet<GridCoord> CollectDoorCoords(IGridManager grid)
        {
            var set = new HashSet<GridCoord>();
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return set;
            var layout = dungeon.CurrentRoomInstance?.SpawnedPrefab != null
                ? dungeon.CurrentRoomInstance.SpawnedPrefab.GetComponent<Rollgeon.Dungeon.Components.RoomLayout>()
                : null;
            if (layout?.DoorSlots == null) return set;
            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.Anchor == null) continue;
                set.Add(grid.WorldToGrid(slot.Anchor.position));
            }
            return set;
        }

        // -----------------------------------------------------------------
        // Resolución
        // -----------------------------------------------------------------

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            if (_active == null || _active.Phase != ChestPhase.Idle) return;
            if (payload.TargetGuid != _active.Guid) return;

            bool fromPlayer = IsPlayer(payload.SourceGuid);

            if (_active.IsMimic)
            {
                // GDD §28/§29 (confirmado): CUALQUIER golpe del jugador activa al
                // Mimic — no solo el letal. El daño no-jugador nunca lo mata (clamp
                // del pipeline), así que acá solo importa el origen.
                if (fromPlayer) ActivateMimic();
                return;
            }

            if (!payload.WasLethal) return;

            if (fromPlayer) OpenChest();
            else BreakChest(payload.SourceGuid);
        }

        private void OpenChest()
        {
            var chest = _active;
            var tierDef = _config.GetTierDef(chest.Tier);
            var rng = _rng ?? new System.Random();

            var loot = _lootPool.Roll(rng, chest.Tier);
            ServiceLocator.TryGetService<IInventoryService>(out var inventory);
            ServiceLocator.TryGetService<IEconomyService>(out var economy);
            var grant = ChestOpenGrant.Grant(inventory, economy, loot, tierDef);

            if (TryGetState(chest.RoomInstanceId, out var state))
            {
                state.Opened = true;
                state.Consumed = true;
                state.LootRolled ??= new List<string>();
                state.LootRolled.Add(grant.IsGold ? $"gold:{grant.GoldAmount}" : grant.Item.ItemId);
            }

            chest.Phase = ChestPhase.Opened;
            DespawnEntity(chest.Guid);

            EventManager.Trigger(EventName.OnChestOpened, chest.Guid, (int)chest.Tier);
            TypedEvent<ChestOpenedPayload>.Raise(new ChestOpenedPayload
            {
                ChestGuid = chest.Guid,
                Tier = chest.Tier,
                Item = grant.Item,
                GoldAmount = grant.GoldAmount,
                WasInventoryFullFallback = grant.WasInventoryFullFallback,
                PoolPreview = _lootPool.GetPoolPreview(chest.Tier),
            });

            _active = null;
        }

        private void BreakChest(Guid sourceGuid)
        {
            var chest = _active;
            if (TryGetState(chest.RoomInstanceId, out var state))
            {
                state.Broken = true;
                state.Consumed = true;
            }

            chest.Phase = ChestPhase.Broken;
            DespawnEntity(chest.Guid);
            EventManager.Trigger(EventName.OnChestBroken, chest.Guid, sourceGuid);
            _active = null;
        }

        /// <summary>
        /// GDD §12.1: el cofre se reemplaza por un enemigo Melee activo en su misma
        /// tile, con vida completa de su propio bloque de stats (escalado por tier,
        /// placeholder TBD-15), que entra a la cola de la ronda EN CURSO sin turno
        /// sorpresa y cuenta para el clear de la sala. Espejo de
        /// <c>AINode_SpawnReinforcements.SpawnWave</c>.
        /// </summary>
        private void ActivateMimic()
        {
            var chest = _active;
            var mimicData = _config.MimicEnemy;
            if (mimicData == null)
            {
                Debug.LogError(LogPrefix + "ChestConfigSO.MimicEnemy sin asignar — el Mimic se abre como cofre normal.");
                chest.IsMimic = false;
                return;
            }

            var tierDef = _config.GetTierDef(chest.Tier);
            float scale = tierDef?.MimicStatScale ?? 1f;
            var coord = chest.Coord;

            // El cofre deja de existir ANTES del spawn: libera la tile y su guid sale
            // del registry (IsChest ya no matchea).
            chest.Phase = ChestPhase.MimicActive;
            DespawnEntity(chest.Guid);

            var mimicId = Guid.NewGuid();
            var mimicAttrs = BuildScaledStats(mimicData, scale, out int maxHp);

            if (ServiceLocator.TryGetService<Combat.Initiative.InMemoryEntityRegistry>(out var registry) && registry != null)
                registry.Register(mimicId, mimicAttrs);
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
                attrs.Register(mimicId, mimicAttrs);
            if (ServiceLocator.TryGetService<Entities.Portraits.IEntityPortraitResolver>(out var portraits) && portraits != null)
                portraits.Register(mimicId, mimicData.Portrait);
            if (ServiceLocator.TryGetService<Combat.AI.IEnemyAIRegistry>(out var aiRegistry) && aiRegistry != null)
                aiRegistry.Register(mimicId, mimicData.CreateRuntimeAIRoot(), maxHp);
            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
                grid.Register(mimicId, coord);

            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                visuals.SpawnEnemy(mimicId, mimicData, coord);
                if (visuals.TryGetPawn(mimicId, out var pawn) && pawn != null)
                {
                    if (pawn.HealthBar != null)
                        pawn.HealthBar.Initialize(mimicId, maxHp, maxHp);
                    // AnimCon_ChestMimic arranca en Idle (cofre cerrado); sin este
                    // trigger el modelo nunca despierta.
                    pawn.TrySetTrigger(MimicAwakenTrigger);
                }
            }

            // A diferencia de los refuerzos, el Mimic SÍ cuenta para el clear: entra a
            // SpawnedEnemies (sin EnemySpawnState — no persiste entre entradas, ver
            // OnCombatEnd) y su muerte lo saca vía DungeonManager.OnEntityDestroyed.
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                && dungeon != null
                && dungeon.GetAllRoomInstances().TryGetValue(chest.RoomInstanceId, out var room))
            {
                room.SpawnedEnemies.Add(mimicId);
            }

            // Recompensa del Mimic: solo oro (GDD §12.1), escalado por tier (TBD-16).
            if (ServiceLocator.TryGetService<EnemyGoldDropService>(out var goldDrops)
                && goldDrops != null
                && tierDef != null)
            {
                var rng = _rng ?? new System.Random();
                int min = Math.Max(0, tierDef.MimicGoldMin);
                int max = Math.Max(min, tierDef.MimicGoldMax);
                goldDrops.RegisterDrop(mimicId, rng.Next(min, max + 1));
            }

            if (ServiceLocator.TryGetService<Combat.TurnOrderService>(out var turnOrder) && turnOrder != null)
                turnOrder.Append(mimicId);

            // Reusa el aviso de refuerzos: TreeDrivenEnemyAI difiere la primera
            // activación — el Mimic aparece sin actuar (GDD §12.1, sin turno sorpresa).
            EventManager.Trigger(EventName.OnReinforcementSpawned, mimicId);

            if (TryGetState(chest.RoomInstanceId, out var state))
            {
                state.MimicActivated = true;
                state.Consumed = true;
            }

            chest.MimicEnemyGuid = mimicId;
            EventManager.Trigger(EventName.OnChestMimicActivated, chest.Guid, mimicId);
        }

        /// <summary>
        /// Stats runtime del Mimic: espejo de <c>EnemyDataSO.CreateRuntimeStats(1)</c>
        /// con vida/ataque/velocidad escaladas ("más débil en las tres dimensiones",
        /// GDD §12.1). Energía y heal quedan en base — el Melee no los usa para pegar.
        /// </summary>
        private static ModifiableAttributes BuildScaledStats(
            Entities.EnemyDataSO data, float scale, out int maxHp)
        {
            maxHp = ScaleStat(data.ResolveMaxHP(1), scale);
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(maxHp));
            attrs.SetAttribute<Attack>(new Attack(ScaleStat(data.BaseAttack, scale)));
            attrs.SetAttribute<Speed>(new Speed(ScaleStat(data.BaseSpeed, scale)));
            attrs.SetAttribute<Energy>(new Energy(data.MaxEnergy));
            attrs.SetAttribute<Entities.Behaviors.HealStrength>(
                new Entities.Behaviors.HealStrength(data.BaseHealStrength));
            attrs.SetAttribute<Shield>(new Shield(0));
            return attrs;
        }

        private static int ScaleStat(int baseValue, float scale) =>
            Math.Max(1, Mathf.RoundToInt(baseValue * scale));

        // -----------------------------------------------------------------
        // Expiración
        // -----------------------------------------------------------------

        private void OnCombatEnd(params object[] args)
        {
            var chest = _active;
            if (chest == null) return;

            if (chest.Phase == ChestPhase.Idle)
            {
                // GDD §26: el combate terminó con el cofre sin resolver (incluye Mimic
                // sin activar) — desaparece sin recompensa, sin ventana de gracia.
                if (TryGetState(chest.RoomInstanceId, out var state)) state.Consumed = true;
                chest.Phase = ChestPhase.Expired;
                DespawnEntity(chest.Guid);
                EventManager.Trigger(EventName.OnChestExpired, chest.Guid);
            }
            else if (chest.Phase == ChestPhase.MimicActive && chest.MimicEnemyGuid != Guid.Empty)
            {
                // Escape con el Mimic vivo: no persiste como EnemySpawnState, así que
                // su guid no puede quedar colgado en SpawnedEnemies para el re-entry.
                if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                    && dungeon != null
                    && dungeon.GetAllRoomInstances().TryGetValue(chest.RoomInstanceId, out var room))
                {
                    room.SpawnedEnemies.Remove(chest.MimicEnemyGuid);
                }
            }

            _active = null;
            _rng = null;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static int CurrentFloorNumber =>
            ServiceLocator.TryGetService<Rollgeon.Run.IRunContextService>(out var runCtx) && runCtx != null
                ? runCtx.FloorIndex + 1
                : 1;

        private static bool IsPlayer(Guid guid) =>
            guid != Guid.Empty
            && ServiceLocator.TryGetService<IPlayerService>(out var player)
            && player != null
            && player.PlayerGuid == guid;

        private static bool TryGetState(Guid roomInstanceId, out ChestState state)
        {
            state = null;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return false;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)) return false;
            return room.ObjectStates.TryGet<ChestState>(StateKey, out state);
        }

        // Idempotente: el teardown de combate puede haber destruido el pawn antes.
        private static void DespawnEntity(Guid guid)
        {
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
                visuals.Despawn(guid);
            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
                grid.Unregister(guid);
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
                attrs.Unregister(guid);
        }
    }
}
