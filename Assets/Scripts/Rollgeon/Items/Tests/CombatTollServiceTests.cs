using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Economy;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Peaje (<see cref="CombatTollService"/>): ofrece solo en salas Combat con el item,
    /// cobra por piso, pagar limpia sin pelear, declinar pelea.
    /// </summary>
    [TestFixture]
    public class CombatTollServiceTests
    {
        private InventoryService _inventory;
        private FakeEconomy _economy;
        private FakeDungeon _dungeon;
        private FakeRun _run;
        private CombatTollService _service;
        private readonly List<(ItemSO item, int cost, bool canAfford)> _prompts = new();
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(Guid.NewGuid()));
            _inventory = new InventoryService(null, 4);
            _economy = new FakeEconomy { Gold = 100 };
            _dungeon = new FakeDungeon();
            _run = new FakeRun();
            _service = new CombatTollService(_inventory, _economy, _dungeon, _run,
                ServiceLocator.GetService<IPlayerService>())
            {
                PromptPresenter = (item, cost, canAfford) => _prompts.Add((item, cost, canAfford)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _inventory?.Dispose();
            _prompts.Clear();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
        }

        private ItemSO AddPeaje(int baseCost = 15, int perFloor = 10)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "peaje";
            item.DisplayName = "Peaje";
            item.Type = ItemType.Passive;
            item.CombatToll = new CombatTollDef { Enabled = true, BaseCost = baseCost, CostPerFloor = perFloor };
            _created.Add(item);
            _inventory.AddItem(item);
            return item;
        }

        private RoomInstance Room(RoomType type, RoomState state = RoomState.Uncleared)
        {
            var so = ScriptableObject.CreateInstance<RoomSO>();
            so.RoomId = type.ToString().ToLowerInvariant();
            so.Type = type;
            _created.Add(so);
            return new RoomInstance { InstanceId = Guid.NewGuid(), Template = so, State = state };
        }

        // ================================================================
        // Cuándo ofrece
        // ================================================================

        [Test]
        public void CostFor_ScalesWithFloor_OneBased()
        {
            var def = new CombatTollDef { Enabled = true, BaseCost = 15, CostPerFloor = 10 };
            Assert.AreEqual(25, def.CostFor(0));
            Assert.AreEqual(35, def.CostFor(1));
            Assert.AreEqual(45, def.CostFor(2));
        }

        [Test]
        public void WithoutItem_DoesNotOffer()
        {
            Assert.IsFalse(_service.TryOffer(Room(RoomType.Combat), () => { }));
            Assert.AreEqual(0, _prompts.Count);
        }

        [Test]
        public void BossOrClearedRoom_NeverOffers()
        {
            AddPeaje();
            Assert.IsFalse(_service.TryOffer(Room(RoomType.Boss), () => { }));
            Assert.IsFalse(_service.TryOffer(Room(RoomType.Combat, RoomState.Cleared), () => { }));
            Assert.IsFalse(_service.TryOffer(Room(RoomType.Shop), () => { }));
            Assert.AreEqual(0, _prompts.Count);
        }

        [Test]
        public void WithItem_OffersTheCostOfTheCurrentFloor()
        {
            AddPeaje();
            _run.FloorIndex = 2;

            Assert.IsTrue(_service.TryOffer(Room(RoomType.Combat), () => { }));

            Assert.AreEqual(1, _prompts.Count);
            Assert.AreEqual(45, _prompts[0].cost);
            Assert.IsTrue(_prompts[0].canAfford);
            Assert.AreEqual(45, _service.PendingCost);
        }

        [Test]
        public void SameRoomTwice_IsIdempotent()
        {
            AddPeaje();
            var room = Room(RoomType.Combat);

            Assert.IsTrue(_service.TryOffer(room, () => { }));
            Assert.IsTrue(_service.TryOffer(room, () => { }));

            Assert.AreEqual(1, _prompts.Count);
        }

        // ================================================================
        // Decisión
        // ================================================================

        [Test]
        public void Accept_SpendsGold_ClearsRoomWithoutCombat_AndAnnounces()
        {
            AddPeaje();
            var room = Room(RoomType.Combat);
            bool fought = false;
            int paid = -1;
            EventManager.Subscribe(EventName.OnCombatTollPaid, args => paid = (int)args[2]);
            _service.TryOffer(room, () => fought = true);

            Assert.IsTrue(_service.AcceptPending());

            Assert.AreEqual(75, _economy.Gold);
            CollectionAssert.AreEqual(new[] { room.InstanceId }, _dungeon.Cleared);
            Assert.IsFalse(fought);
            Assert.AreEqual(25, paid);
            Assert.IsFalse(_service.HasPendingOffer);
        }

        [Test]
        public void Accept_WithoutGold_KeepsTheOfferPending()
        {
            AddPeaje();
            _economy.Gold = 10;
            _service.TryOffer(Room(RoomType.Combat), () => { });
            Assert.IsFalse(_prompts[0].canAfford);

            Assert.IsFalse(_service.AcceptPending());

            Assert.AreEqual(10, _economy.Gold);
            Assert.AreEqual(0, _dungeon.Cleared.Count);
            Assert.IsTrue(_service.HasPendingOffer);
        }

        [Test]
        public void Decline_FightsAndClearsPending()
        {
            AddPeaje();
            bool fought = false;
            _service.TryOffer(Room(RoomType.Combat), () => fought = true);

            _service.DeclinePending();

            Assert.IsTrue(fought);
            Assert.AreEqual(100, _economy.Gold);
            Assert.AreEqual(0, _dungeon.Cleared.Count);
            Assert.IsFalse(_service.HasPendingOffer);
        }

        [Test]
        public void EnteringAnotherRoom_CancelsThePendingOffer()
        {
            AddPeaje();
            bool fought = false;
            var room = Room(RoomType.Combat);
            _service.TryOffer(room, () => fought = true);

            EventManager.Trigger(EventName.OnRoomEntered, room.InstanceId, "combat");
            Assert.IsTrue(_service.HasPendingOffer, "misma sala: sigue pendiente");

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "otra");
            Assert.IsFalse(_service.HasPendingOffer);
            Assert.IsFalse(fought);
        }

        // ================================================================
        // Fakes
        // ================================================================

        private sealed class FakeEconomy : IEconomyService
        {
            public int Gold;
            public int CurrentGold => Gold;
            public void Add(int amount) => Gold += amount;
            public bool Spend(int amount)
            {
                if (Gold < amount) return false;
                Gold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => Gold >= amount;
            public void ResetTo(int amount) => Gold = amount;
        }

        private sealed class FakeRun : IRunContextService
        {
            public Guid RunId => Guid.Empty;
            public int FloorIndex { get; set; }
            public ClassHeroSO SelectedHero => null;
            public bool IsRunActive => true;
            public void AdvanceFloor() => FloorIndex++;
        }

        private sealed class FakeDungeon : IDungeonService
        {
            public readonly List<Guid> Cleared = new();
            public RoomInstance CurrentRoomInstance { get; set; }
            public RoomSO CurrentRoom => CurrentRoomInstance?.Template;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid id) { id = Guid.Empty; return false; }
            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(DoorDirection direction) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public bool SetRoomState(Guid id, RoomState state) => false;
            public bool MarkRoomCleared(Guid id) { Cleared.Add(id); return true; }
            public void ResyncDoorVisuals(Guid id) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders()
                => Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public StubPlayerService(Guid guid) { PlayerGuid = guid; }
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }
    }
}
