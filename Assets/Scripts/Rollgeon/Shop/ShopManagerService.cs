using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Implementación MVP de <see cref="IShopManagerService"/>. Lazy-init por
    /// room vía <c>OnRoomEntered</c>, rolling contra <see cref="ShopPoolSO"/>,
    /// persistencia en <c>RoomInstance.ObjectStates</c> como
    /// <see cref="ShopItemState"/> (§13.6). TECHNICAL.md §17.F.
    /// </summary>
    public sealed class ShopManagerService : IShopManagerService, IDisposable
    {
        private const string LogPrefix = "[ShopManagerService] ";
        private const string SpawnPointPrefix = "shop_";

        private readonly ShopConfigSO _config;
        private readonly ShopPoolSO _pool;

        // Override del tutorial (tienda de 1 item). El service es Global, así que
        // el teardown del tutorial debe llamar ClearTutorialOverride().
        private ShopConfigSO _overrideConfig;
        private ShopPoolSO _overridePool;

        private ShopConfigSO ActiveConfig => _overrideConfig != null ? _overrideConfig : _config;
        private ShopPoolSO ActivePool => _overridePool != null ? _overridePool : _pool;

        private readonly Dictionary<Guid, List<ShopSlot>> _slotsByRoom = new Dictionary<Guid, List<ShopSlot>>();
        private readonly HashSet<Guid> _initialized = new HashSet<Guid>();

        private EventManager.EventReceiver _onRoomEnteredHandler;

        public ShopManagerService(ShopConfigSO config, ShopPoolSO pool)
        {
            _config = config;
            _pool = pool;

            _onRoomEnteredHandler = OnRoomEntered;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
        }

        public void Dispose()
        {
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }
            _slotsByRoom.Clear();
            _initialized.Clear();
        }

        public IReadOnlyList<ShopSlot> GetSlots(Guid roomInstanceId)
        {
            return _slotsByRoom.TryGetValue(roomInstanceId, out var list)
                ? list
                : Array.Empty<ShopSlot>();
        }

        public bool IsInitialized(Guid roomInstanceId) => _initialized.Contains(roomInstanceId);

        public ShopSlot FindActiveSlot(Guid roomInstanceId, string spawnPointId)
        {
            if (!_slotsByRoom.TryGetValue(roomInstanceId, out var list)) return null;
            foreach (var slot in list)
            {
                if (slot.Purchased) continue;
                if (slot.SpawnPointId == spawnPointId) return slot;
            }
            return null;
        }

        public void NotifyItemPurchased(Guid roomInstanceId, string spawnPointId, int pricePaid)
        {
            var slot = FindActiveSlot(roomInstanceId, spawnPointId);
            if (slot == null) return;

            slot.Purchased = true;

            // Persistir en ObjectStates — fuente de verdad para re-entry.
            if (TryGetDungeonService(out var dungeon)
                && dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)
                && room.ObjectStates.TryGet<ShopItemState>(spawnPointId, out var state))
            {
                state.Purchased = true;
                state.Consumed = true;
            }

            if (slot.SpawnedVisual != null)
            {
                UnityEngine.Object.Destroy(slot.SpawnedVisual);
                slot.SpawnedVisual = null;
            }

            var entryId = slot.Item != null ? slot.Item.EntryId : string.Empty;
            EventManager.Trigger(EventName.OnShopItemPurchased, spawnPointId, entryId, pricePaid);
        }

        public bool CanRestock(Guid roomInstanceId) => ActiveConfig != null && ActiveConfig.AllowRestock;

        public void Restock(Guid roomInstanceId)
        {
            // MVP: no wired. El RestockMachine prop + EffRestockShop quedan para
            // un follow-up (§17.F.5). Log + no-op para no explotar si alguien invoca.
            Debug.LogWarning(LogPrefix + "Restock invocado pero el MVP no lo implementa — follow-up §17.F.5.");
        }

        public void Initialize(RoomInstance room, int floorDepth)
        {
            InitializeInternal(room, floorDepth);
        }

        public void SetTutorialOverride(ShopConfigSO config, ShopPoolSO pool)
        {
            _overrideConfig = config;
            _overridePool = pool;
        }

        public void ClearTutorialOverride()
        {
            _overrideConfig = null;
            _overridePool = null;
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private void OnRoomEntered(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid roomId) return;
            if (_initialized.Contains(roomId)) return;

            if (!TryGetDungeonService(out var dungeon)) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return;
            if (room.Template == null || room.Template.Type != RoomType.Shop) return;

            // #158: floor depth real desde el RunContext (antes hardcodeado a 0).
            int floorDepth = ServiceLocator.TryGetService<Rollgeon.Run.IRunContextService>(out var runCtx)
                ? runCtx.FloorIndex
                : 0;
            InitializeInternal(room, floorDepth);
        }

        private void InitializeInternal(RoomInstance room, int floorDepth)
        {
            if (room == null) return;
            if (_initialized.Contains(room.InstanceId)) return;
            if (ActiveConfig == null || ActivePool == null)
            {
                Debug.LogError(LogPrefix + "ShopConfigSO o ShopPoolSO ausentes — no se inicializa la shop.");
                return;
            }

            var spawnPoints = ResolveRewardSpawnPoints(room);
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning(LogPrefix + $"La shop room '{room.Template?.RoomId}' no tiene RewardSpawnPoints — sin slots.");
                _initialized.Add(room.InstanceId);
                return;
            }

            int slotCount = Mathf.Min(spawnPoints.Count, Mathf.Max(1, ActiveConfig.MaxItemSlots));
            // Seed derivado del seed del piso + celda (estables entre regeneraciones)
            // y NO del InstanceId (Guid nuevo por GenerateFloor): un piso resumido
            // desde save restockea la tienda idéntica.
            var rng = new System.Random(DeriveShopSeed(room));
            var slots = new List<ShopSlot>(slotCount);
            // Tracks entries ya rolleadas en esta tienda — pasadas como exclude a Roll()
            // para que cada slot tenga un ítem distinto (mientras el pool tenga variedad).
            var rolledInThisShop = new HashSet<IShopRewardEntry>();

            for (int i = 0; i < slotCount; i++)
            {
                string spawnPointId = SpawnPointKey(i);
                // Slot 0 = entry garantizada (la poción, si el pool la cablea);
                // el resto rolea del pool dinámico de pasivas + extras manuales.
                bool guaranteedSlot = i == 0;
                var slot = BuildOrHydrateSlot(room, spawnPointId, rng, floorDepth, rolledInThisShop, guaranteedSlot);
                if (slot == null) continue;

                if (!slot.Purchased)
                {
                    SpawnPedestalVisual(slot, room, spawnPoints[i]);
                }
                if (slot.Item != null) rolledInThisShop.Add(slot.Item);

                slots.Add(slot);
            }

            _slotsByRoom[room.InstanceId] = slots;
            _initialized.Add(room.InstanceId);
        }

        private int DeriveShopSeed(RoomInstance room)
        {
            if (!TryGetDungeonService(out var dungeon))
                return room.InstanceId.GetHashCode();
            return DeriveShopSeed(dungeon.CurrentFloorSeed, room.GridCell);
        }

        /// <summary>Puro para tests: mismo (floorSeed, celda) → mismo stock.</summary>
        public static int DeriveShopSeed(int floorSeed, Vector2Int cell)
        {
            unchecked
            {
                // Mismo estilo que FloorProgressionService.DeriveSeed.
                return floorSeed * 92821 + cell.x * 31 + cell.y;
            }
        }

        private ShopSlot BuildOrHydrateSlot(
            RoomInstance room, string spawnPointId, System.Random rng, int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> rolledInThisShop, bool guaranteedSlot)
        {
            if (room.ObjectStates.TryGet<ShopItemState>(spawnPointId, out var state))
            {
                // Re-entry: hidratamos desde el state persistido. No re-rolear.
                var entry = ResolveEntryFromPool(state.ReservedItemId);
                if (entry == null)
                {
                    Debug.LogWarning(LogPrefix + $"ReservedItemId '{state.ReservedItemId}' no encontrado en el pool — slot se omite.");
                    return null;
                }
                return new ShopSlot
                {
                    SpawnPointId = spawnPointId,
                    Item = entry,
                    Price = state.ReservedPrice,
                    Purchased = state.Purchased,
                };
            }

            // Primera visita: rolear + persistir. Pasamos los rolled previos como
            // exclude para evitar duplicados dentro del mismo shop. El slot
            // garantizado (poción) saltea el roll; sin garantizado cableado (ej.
            // tutorial) degrada al roll normal.
            ShopRollResult rolled;
            if (!guaranteedSlot || !ActivePool.TryGetGuaranteed(out rolled))
            {
                rolled = ActivePool.RollDynamic(rng, floorDepth, rolledInThisShop);
            }
            if (rolled.Item == null)
            {
                Debug.LogWarning(LogPrefix + "Pool vacío o sin entries eligibles — slot se omite.");
                return null;
            }

            int price = ActiveConfig.ResolvePrice(rolled.BasePrice, rng);
            var newState = new ShopItemState
            {
                SpawnPointId = spawnPointId,
                ReservedItemId = rolled.Item.EntryId,
                ReservedPrice = price,
                Purchased = false,
                Consumed = false,
            };
            room.ObjectStates.Set(spawnPointId, newState);

            return new ShopSlot
            {
                SpawnPointId = spawnPointId,
                Item = rolled.Item,
                Price = price,
                Purchased = false,
            };
        }

        private void SpawnPedestalVisual(ShopSlot slot, RoomInstance room, Transform spawnPoint)
        {
            if (ActiveConfig.PedestalPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "ShopConfigSO.PedestalPrefab sin asignar — no se instancia visual.");
                return;
            }
            if (spawnPoint == null) return;

            Transform parent = room.SpawnedPrefab != null ? room.SpawnedPrefab.transform : null;
            var go = UnityEngine.Object.Instantiate(ActiveConfig.PedestalPrefab, spawnPoint.position, spawnPoint.rotation, parent);
            go.name = $"[ShopPedestal] {slot.Item?.DisplayName ?? slot.Item?.EntryId ?? "?"}";

            var pedestal = go.GetComponent<ShopItemPedestalInteractable>();
            if (pedestal == null)
            {
                Debug.LogError(LogPrefix + "PedestalPrefab no tiene ShopItemPedestalInteractable — no se puede cablear la compra.");
            }
            else
            {
                pedestal.Configure(room.InstanceId, slot, this);
            }

            SpawnItemVisualOnTop(go.transform, slot);
            slot.SpawnedVisual = go;
        }

        /// <summary>
        /// Instancia el visual 3D del reward como hijo del pedestal. Dispatch por
        /// tipo: <see cref="ShopItemDef"/> usa el <see cref="ItemSO.WorldPrefab"/>
        /// resolved via catálogo; <see cref="Upgrades.Combos.ComboPassiveSO"/> usa
        /// su propio <c>WorldPrefab</c>. Posiciona via
        /// <see cref="ShopConfigSO.ItemVisualLocalOffset"/> (default Y=1.5).
        /// </summary>
        private void SpawnItemVisualOnTop(Transform pedestalRoot, ShopSlot slot)
        {
            if (pedestalRoot == null || slot?.Item == null) return;

            var prefab = ResolveWorldPrefab(slot.Item);
            if (prefab == null) return;

            var visual = UnityEngine.Object.Instantiate(prefab, pedestalRoot);
            visual.transform.localPosition = ActiveConfig != null ? ActiveConfig.ItemVisualLocalOffset : new Vector3(0f, 1.5f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            string displayName = slot.Item.DisplayName ?? slot.Item.EntryId ?? "?";
            visual.name = $"[ShopItemVisual] {displayName}";
        }

        /// <summary>
        /// Dispatch polimórfico del WorldPrefab. Cuando se agregue un nuevo tipo
        /// de <see cref="IShopRewardEntry"/>, sumar el case.
        /// </summary>
        private static GameObject ResolveWorldPrefab(IShopRewardEntry entry)
        {
            switch (entry)
            {
                case ShopItemDef itemDef:
                    if (!ServiceLocator.TryGetService<ItemCatalogSO>(out var catalog) || catalog == null) return null;
                    var itemSo = catalog.GetById(itemDef.ItemId);
                    return itemSo != null ? itemSo.WorldPrefab : null;
                case Rollgeon.Upgrades.Combos.ComboPassiveSO passive:
                    return passive.WorldPrefab;
                default:
                    return null;
            }
        }

        private List<Transform> ResolveRewardSpawnPoints(RoomInstance room)
        {
            var list = new List<Transform>();
            if (room?.SpawnedPrefab == null) return list;

            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null || layout.RewardSpawnPoints == null) return list;

            foreach (var t in layout.RewardSpawnPoints)
            {
                if (t != null) list.Add(t);
            }
            return list;
        }

        private IShopRewardEntry ResolveEntryFromPool(string entryId)
        {
            if (ActivePool == null || string.IsNullOrEmpty(entryId)) return null;

            var guaranteed = ActivePool.Guaranteed.GetEntry();
            if (guaranteed != null && guaranteed.EntryId == entryId) return guaranteed;

            if (ActivePool.Items != null)
            {
                foreach (var weighted in ActivePool.Items)
                {
                    var entry = weighted.GetEntry();
                    if (entry == null) continue;
                    if (entry.EntryId == entryId) return entry;
                }
            }

            // Pasivas del pool dinámico (re-entry de un slot roleado de ahí).
            if (ActivePool.PassivePool != null && ActivePool.PassivePool.Entries != null)
            {
                foreach (var entry in ActivePool.PassivePool.Entries)
                {
                    var passive = entry?.Passive;
                    if (passive == null) continue;
                    if (((IShopRewardEntry)passive).EntryId == entryId) return passive;
                }
            }

            return null;
        }

        private static bool TryGetDungeonService(out IDungeonService dungeon)
            => ServiceLocator.TryGetService(out dungeon);

        private static string SpawnPointKey(int index) => SpawnPointPrefix + index;
    }
}
