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
    public sealed class ChestService : IChestService, IDisposable
    {
        private const string LogPrefix = "[ChestService] ";
        private const string StateKey = "chest_0";

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
                var pawn = visuals.SpawnProp(guid, _config.ChestPrefab, coord.Value);
                if (pawn != null)
                {
                    TintChest(pawn.gameObject, tierDef.BodyColor, _config.FittingsColor);
                    if (pawn.HealthBar != null)
                        pawn.HealthBar.Initialize(guid, tierDef.MaxHP, tierDef.MaxHP);
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
                candidates.Add(coord);
            }

            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Tinta por nombre de renderer: 'Body' recibe el color del tier, 'Fittings' el
        // color de herrajes; cualquier otro renderer queda como está. Vía
        // MaterialPropertyBlock para no instanciar materiales.
        private static void TintChest(GameObject go, Color bodyColor, Color fittingsColor)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Color? tint = null;
                if (r.gameObject.name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                    tint = bodyColor;
                else if (r.gameObject.name.IndexOf("Fittings", StringComparison.OrdinalIgnoreCase) >= 0)
                    tint = fittingsColor;
                if (tint == null) continue;

                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, tint.Value);
                mpb.SetColor(ColorId, tint.Value);
                r.SetPropertyBlock(mpb);
            }
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

        private void ActivateMimic()
        {
            // Implementación en el commit de Mimic — hasta entonces un Mimic golpeado
            // por el jugador se comporta como cofre normal para no dejar un soft-state.
            var chest = _active;
            Debug.LogWarning(LogPrefix + "Mimic golpeado por el jugador — activación pendiente de implementación.");
            chest.IsMimic = false;
        }

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
