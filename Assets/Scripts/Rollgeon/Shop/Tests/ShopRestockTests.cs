using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Economy;
using Rollgeon.GameCamera;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Shop.Tests
{
    /// <summary>
    /// Máquina de reroll (§17.F.5): fórmula compuesta de costo (misma que el
    /// altar: base × mult^usos, Ceil) y <see cref="ShopManagerService.TryRestock"/> —
    /// re-rolea TODOS los slots incluidos los comprados (Isaac real), respeta
    /// <c>MaxRestocks</c> (≤0 = infinitos), cobra vía <see cref="IEconomyService"/>
    /// (all-or-nothing) y persiste usos + stock en <c>ObjectStates</c>.
    /// </summary>
    [TestFixture]
    public class ShopRestockTests
    {
        private readonly List<Object> _assets = new List<Object>();
        private readonly List<GameObject> _gos = new List<GameObject>();

        private ShopConfigSO _config;
        private ShopPoolSO _pool;
        private ShopManagerService _service;
        private StubDungeon _dungeon;
        private RoomInstance _room;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();

            _config = ScriptableObject.CreateInstance<ShopConfigSO>();
            _assets.Add(_config);
            _config.MaxItemSlots = 2;
            _config.AllowRestock = true;
            _config.MaxRestocks = 0;
            _config.RestockCost = 10;
            _config.RestockCostMultiplier = 2f;

            _pool = ScriptableObject.CreateInstance<ShopPoolSO>();
            _assets.Add(_pool);
            _pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = NewItem("item.a"), Weight = 1f, BasePrice = 5 },
                new WeightedShopItem { Item = NewItem("item.b"), Weight = 1f, BasePrice = 5 },
                new WeightedShopItem { Item = NewItem("item.c"), Weight = 1f, BasePrice = 5 },
                new WeightedShopItem { Item = NewItem("item.d"), Weight = 1f, BasePrice = 5 },
            };

            _room = NewShopRoom(spawnPoints: 2);
            _dungeon = new StubDungeon { Room = _room, FloorSeed = 777 };
            ServiceLocator.AddService<IDungeonService>(_dungeon);

            _service = new ShopManagerService(_config, _pool);
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.Clear();
            foreach (var go in _gos) if (go != null) Object.DestroyImmediate(go);
            _gos.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ---------------- Fórmula de costo ----------------

        [Test]
        public void ResolveRestockCost_ZeroUses_ReturnsBase()
        {
            _config.RestockCost = 10;
            _config.RestockCostMultiplier = 1.5f;

            Assert.AreEqual(10, _config.ResolveRestockCost(0));
        }

        [Test]
        public void ResolveRestockCost_CompoundsPerUse_WithCeil()
        {
            _config.RestockCost = 10;
            _config.RestockCostMultiplier = 1.5f;

            Assert.AreEqual(15, _config.ResolveRestockCost(1)); // 10 × 1.5
            Assert.AreEqual(23, _config.ResolveRestockCost(2)); // 10 × 2.25 = 22.5 → Ceil
        }

        [Test]
        public void ResolveRestockCost_MultiplierOne_StaysFlat()
        {
            _config.RestockCost = 10;
            _config.RestockCostMultiplier = 1f;

            Assert.AreEqual(10, _config.ResolveRestockCost(0));
            Assert.AreEqual(10, _config.ResolveRestockCost(5));
        }

        // ---------------- Gates ----------------

        [Test]
        public void CanRestock_DisallowedByConfig_False()
        {
            _config.AllowRestock = false;

            Assert.IsFalse(_service.CanRestock(_room.InstanceId));
            Assert.IsFalse(_service.TryRestock(_room.InstanceId));
        }

        [Test]
        public void TryRestock_RespectsMaxRestocks()
        {
            _config.MaxRestocks = 1;
            _service.Initialize(_room, 0);

            Assert.IsTrue(_service.TryRestock(_room.InstanceId), "primer uso");
            Assert.IsFalse(_service.CanRestock(_room.InstanceId), "agotada");
            Assert.IsFalse(_service.TryRestock(_room.InstanceId), "segundo uso rechazado");
        }

        [Test]
        public void TryRestock_InsufficientGold_FailsWithoutCharging()
        {
            var economy = new FakeEconomy(5); // costo base = 10
            ServiceLocator.AddService<IEconomyService>(economy);
            _service.Initialize(_room, 0);

            Assert.IsFalse(_service.TryRestock(_room.InstanceId));
            Assert.AreEqual(5, economy.CurrentGold, "no cobra parcial");
            Assert.AreEqual(10, _service.GetRestockCost(_room.InstanceId), "usos siguen en 0");
        }

        [Test]
        public void TryRestock_ChargesCompoundCostPerUse()
        {
            var economy = new FakeEconomy(100);
            ServiceLocator.AddService<IEconomyService>(economy);
            _service.Initialize(_room, 0);

            Assert.IsTrue(_service.TryRestock(_room.InstanceId));
            Assert.AreEqual(90, economy.CurrentGold, "primer uso cobra el base (10)");

            Assert.IsTrue(_service.TryRestock(_room.InstanceId));
            Assert.AreEqual(70, economy.CurrentGold, "segundo uso cobra 10 × 2^1 = 20");
        }

        // ---------------- Re-roll ----------------

        [Test]
        public void TryRestock_RerollsAllSlots_IncludingPurchased()
        {
            _service.Initialize(_room, 0);
            var before = _service.GetSlots(_room.InstanceId);
            Assert.AreEqual(2, before.Count, "sanity: 2 slots iniciales");

            _service.NotifyItemPurchased(_room.InstanceId, before[0].SpawnPointId, before[0].Price);
            Assert.IsTrue(before[0].Purchased, "sanity: slot 0 comprado");

            Assert.IsTrue(_service.TryRestock(_room.InstanceId));

            var after = _service.GetSlots(_room.InstanceId);
            Assert.AreEqual(2, after.Count, "el slot comprado también se rellena (Isaac real)");
            foreach (var slot in after)
            {
                Assert.IsFalse(slot.Purchased, $"{slot.SpawnPointId} vuelve a estar en venta");
                Assert.IsNotNull(slot.Item, $"{slot.SpawnPointId} tiene ítem");
            }
        }

        [Test]
        public void TryRestock_PersistsUsesAndFreshStockInObjectStates()
        {
            _service.Initialize(_room, 0);
            var before = _service.GetSlots(_room.InstanceId);
            _service.NotifyItemPurchased(_room.InstanceId, before[0].SpawnPointId, before[0].Price);

            Assert.IsTrue(_service.TryRestock(_room.InstanceId));

            Assert.IsTrue(_room.ObjectStates.TryGet<ShopRestockState>("shop_restock", out var restock));
            Assert.AreEqual(1, restock.Uses);

            var after = _service.GetSlots(_room.InstanceId);
            foreach (var slot in after)
            {
                Assert.IsTrue(_room.ObjectStates.TryGet<ShopItemState>(slot.SpawnPointId, out var state));
                Assert.IsFalse(state.Purchased, $"state de {slot.SpawnPointId} pisado por el restock");
                Assert.AreEqual(slot.Item.EntryId, state.ReservedItemId, "stock nuevo persistido");
            }
        }

        [Test]
        public void TryRestock_NoDuplicateItemsWithinShop()
        {
            _service.Initialize(_room, 0);

            Assert.IsTrue(_service.TryRestock(_room.InstanceId));

            var slots = _service.GetSlots(_room.InstanceId);
            var ids = new HashSet<string>();
            foreach (var slot in slots)
                Assert.IsTrue(ids.Add(slot.Item.EntryId), $"ítem repetido: {slot.Item.EntryId}");
        }

        // ---------------- Helpers ----------------

        private ItemSO NewItem(string itemId)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = itemId;
            item.DisplayName = itemId;
            _assets.Add(item);
            return item;
        }

        private RoomInstance NewShopRoom(int spawnPoints)
        {
            var template = ScriptableObject.CreateInstance<RoomSO>();
            _assets.Add(template);

            var prefab = new GameObject("[Test] ShopRoom");
            _gos.Add(prefab);
            var layout = prefab.AddComponent<RoomLayout>();
            layout.RewardSpawnPoints = new List<Transform>();
            for (int i = 0; i < spawnPoints; i++)
            {
                var point = new GameObject($"Item{i + 1}").transform;
                point.SetParent(prefab.transform, worldPositionStays: false);
                point.localPosition = new Vector3(i * 2f, 0.5f, 0f);
                layout.RewardSpawnPoints.Add(point);
            }

            return new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = template,
                SpawnedPrefab = prefab,
                GridCell = new Vector2Int(3, 1),
            };
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public int CurrentGold { get; private set; }
            public FakeEconomy(int start) { CurrentGold = start; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= CurrentGold;
            public void ResetTo(int amount) { CurrentGold = amount < 0 ? 0 : amount; }
        }

        private sealed class StubDungeon : IDungeonService
        {
            public RoomInstance Room;
            public int FloorSeed;

            public RoomSO CurrentRoom => Room?.Template;
            public RoomInstance CurrentRoomInstance => Room;
            public int CurrentFloorSeed => FloorSeed;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances()
            {
                var dict = new Dictionary<Guid, RoomInstance>();
                if (Room != null) dict[Room.InstanceId] = Room;
                return dict;
            }

            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
            {
                neighborInstanceId = Guid.Empty;
                return false;
            }
            public bool EnterRoomByDoor(DoorDirection direction) => false;
            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByInstanceId(Guid instanceId) => false;
            public bool SetRoomState(Guid instanceId, RoomState state) => false;
            public void ResyncDoorVisuals(Guid instanceId) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }
    }
}
