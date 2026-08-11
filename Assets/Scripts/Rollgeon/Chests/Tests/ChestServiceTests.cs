using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    /// <summary>
    /// Tests del núcleo de <see cref="ChestService"/>: spawn determinista al
    /// OnCombatStart, resolución por origen del golpe letal y expiración en
    /// OnCombatEnd. El daño se inyecta crudo vía TypedEvent, igual que en
    /// CombatDeathWatcherTests.
    /// </summary>
    [TestFixture]
    public class ChestServiceTests
    {
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        private ChestConfigSO _config;
        private ChestLootPoolSO _pool;
        private ChestService _service;
        private AttributesManager _attrs;
        private GridManager _grid;
        private StubDungeon _dungeon;
        private StubPlayerService _player;
        private FakeInventoryService _inventory;
        private FakeEconomyService _economy;
        private RoomInstance _room;
        private RoomSO _combatRoom;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<ChestOpenedPayload>.Clear();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            ServiceLocator.AddService<IGridManager>(_grid);

            _player = new StubPlayerService { PlayerGuid = Guid.NewGuid() };
            ServiceLocator.AddService<IPlayerService>(_player);

            _inventory = new FakeInventoryService();
            _economy = new FakeEconomyService();
            ServiceLocator.AddService<Rollgeon.Items.IInventoryService>(_inventory);
            ServiceLocator.AddService<Rollgeon.Economy.IEconomyService>(_economy);

            _combatRoom = ScriptableObject.CreateInstance<RoomSO>();
            _combatRoom.Type = RoomType.Combat;
            _assets.Add(_combatRoom);

            _room = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = _combatRoom,
                GridCell = new Vector2Int(1, 2),
                State = RoomState.Uncleared,
            };
            _dungeon = new StubDungeon { Room = _room, FloorSeed = 555 };
            ServiceLocator.AddService<IDungeonService>(_dungeon);

            _config = ScriptableObject.CreateInstance<ChestConfigSO>();
            _config.SpawnFrequency = 1f;  // spawn garantizado salvo test específico
            _config.MimicSpawnChance = 0f;
            foreach (ItemRarity tier in Enum.GetValues(typeof(ItemRarity)))
            {
                _config.Tiers.Add(new ChestTierDef
                {
                    Tier = tier,
                    MaxHP = 20,
                    FallbackGold = 5,
                });
            }
            _assets.Add(_config);

            _pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            foreach (ItemRarity tier in Enum.GetValues(typeof(ItemRarity)))
            {
                _pool.Buckets.Add(new ChestLootBucket { Tier = tier, GoldMin = 10, GoldMax = 10 });
            }
            _assets.Add(_pool);

            _service = new ChestService(_config, _pool);
            ServiceLocator.AddService<IChestService>(_service);
            ServiceLocator.AddService<IChestRegistry>(_service);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _attrs?.Dispose();
            foreach (var asset in _assets)
            {
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            _assets.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<ChestOpenedPayload>.Clear();
        }

        // ----- helpers ---------------------------------------------------

        private void StartCombat() =>
            EventManager.Trigger(EventName.OnCombatStart, _room.InstanceId);

        private void EndCombat() =>
            EventManager.Trigger(EventName.OnCombatEnd, _room.InstanceId);

        private void Hit(Guid source, bool lethal)
        {
            Assert.IsNotNull(_service.ActiveChest, "precondición: hay cofre activo");
            var target = _service.ActiveChest.Guid;
            if (lethal) _attrs.SetAttributeValue<Health, int>(target, 0);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = source,
                TargetGuid = target,
                FinalDamage = lethal ? 99 : 3,
                WasLethal = lethal,
            });
        }

        private ChestState State()
        {
            Assert.IsTrue(_room.ObjectStates.TryGet<ChestState>("chest_0", out var state));
            return state;
        }

        // ----- spawn -----------------------------------------------------

        [Test]
        public void OnCombatStart_ShouldSpawnChest_WhenRollSucceeds()
        {
            // Arrange en SetUp (SpawnFrequency = 1).
            Guid spawnedGuid = Guid.Empty;
            EventManager.Subscribe(EventName.OnChestSpawned, args => spawnedGuid = (Guid)args[0]);

            // Act
            StartCombat();

            // Assert — registrado como entidad completa.
            Assert.IsNotNull(_service.ActiveChest);
            Assert.AreEqual(spawnedGuid, _service.ActiveChest.Guid);
            Assert.IsTrue(_service.IsChest(spawnedGuid));
            Assert.IsNotNull(_attrs.GetAttribute<Health>(spawnedGuid));
            Assert.AreEqual(20, _attrs.GetAttribute<Health>(spawnedGuid).Value);
            Assert.IsTrue(_grid.TryGetPosition(spawnedGuid, out _));
            Assert.IsTrue(State().Spawned);
        }

        [Test]
        public void OnCombatStart_ShouldNotSpawn_WhenRollFails()
        {
            // Arrange
            _config.SpawnFrequency = 0f;

            // Act
            StartCombat();

            // Assert — el roll queda persistido igual (no se re-rollea al volver).
            Assert.IsNull(_service.ActiveChest);
            Assert.IsFalse(State().Spawned);
        }

        [Test]
        public void OnCombatStart_ShouldNotSpawn_WhenRoomIsNotCombat()
        {
            // Arrange
            _combatRoom.Type = RoomType.Shop;

            // Act
            StartCombat();

            // Assert — ni cofre ni estado.
            Assert.IsNull(_service.ActiveChest);
            Assert.IsFalse(_room.ObjectStates.TryGet<ChestState>("chest_0", out _));
        }

        [Test]
        public void OnCombatStart_ShouldHydrateSameRoll_OnReentry()
        {
            // Arrange — primer combate rollea y persiste.
            StartCombat();
            int tierFirst = State().TierIndex;
            EndCombat(); // expira y consume

            // Act — re-entrada: estado Consumed ⇒ no respawnea.
            StartCombat();

            // Assert
            Assert.IsNull(_service.ActiveChest);
            Assert.AreEqual(tierFirst, State().TierIndex);
            Assert.IsTrue(State().Consumed);
        }

        [Test]
        public void OnCombatStart_ShouldProduceSameRoll_ForSameSeedAndCell()
        {
            // Arrange — dos services vírgenes sobre la misma sala/seed deben rollear igual.
            StartCombat();
            var first = State();
            bool firstSpawned = first.Spawned;
            int firstTier = first.TierIndex;
            bool firstMimic = first.IsMimic;

            // Act — reset completo del estado de la sala, mismo seed.
            EndCombat();
            _room.ObjectStates.Remove("chest_0");
            StartCombat();

            // Assert
            var second = State();
            Assert.AreEqual(firstSpawned, second.Spawned);
            Assert.AreEqual(firstTier, second.TierIndex);
            Assert.AreEqual(firstMimic, second.IsMimic);
        }

        // ----- resolución ------------------------------------------------

        [Test]
        public void PlayerLethalHit_ShouldOpenChest_AndGrantReward()
        {
            // Arrange
            StartCombat();
            var chestGuid = _service.ActiveChest.Guid;
            ChestOpenedPayload? payload = null;
            TypedEvent<ChestOpenedPayload>.Subscribe(p => payload = p);

            // Act
            Hit(_player.PlayerGuid, lethal: true);

            // Assert — abierto: reward otorgado (pool solo-oro: 10), payload para UI,
            // estado consumido y entidad desregistrada.
            Assert.IsNull(_service.ActiveChest);
            Assert.IsTrue(payload.HasValue);
            Assert.AreEqual(chestGuid, payload.Value.ChestGuid);
            Assert.IsTrue(payload.Value.GoldAmount > 0 || payload.Value.Item != null);
            Assert.AreEqual(10, _economy.CurrentGold);
            Assert.IsTrue(State().Opened);
            Assert.IsTrue(State().Consumed);
            Assert.IsFalse(_grid.TryGetPosition(chestGuid, out _));
            Assert.IsNull(_attrs.GetAttribute<Health>(chestGuid));
            Assert.IsFalse(_service.IsChest(chestGuid));
        }

        [Test]
        public void EnemyLethalHit_ShouldBreakChest_WithoutReward()
        {
            // Arrange
            StartCombat();
            var chestGuid = _service.ActiveChest.Guid;
            var enemySource = Guid.NewGuid();
            Guid brokenBy = Guid.Empty;
            EventManager.Subscribe(EventName.OnChestBroken, args => brokenBy = (Guid)args[1]);

            // Act
            Hit(enemySource, lethal: true);

            // Assert — roto sin recompensa.
            Assert.IsNull(_service.ActiveChest);
            Assert.AreEqual(enemySource, brokenBy);
            Assert.AreEqual(0, _economy.CurrentGold);
            Assert.AreEqual(0, _inventory.Added.Count);
            Assert.IsTrue(State().Broken);
            Assert.IsTrue(State().Consumed);
            Assert.IsFalse(State().Opened);
        }

        [Test]
        public void NonLethalHit_ShouldKeepChestAlive()
        {
            // Arrange
            StartCombat();
            var chestGuid = _service.ActiveChest.Guid;

            // Act
            Hit(_player.PlayerGuid, lethal: false);

            // Assert
            Assert.IsNotNull(_service.ActiveChest);
            Assert.IsTrue(_service.IsChest(chestGuid));
            Assert.IsFalse(State().Consumed);
        }

        // ----- expiración ------------------------------------------------

        [Test]
        public void OnCombatEnd_ShouldExpireUnresolvedChest_WithoutReward()
        {
            // Arrange
            StartCombat();
            var chestGuid = _service.ActiveChest.Guid;
            Guid expiredGuid = Guid.Empty;
            EventManager.Subscribe(EventName.OnChestExpired, args => expiredGuid = (Guid)args[0]);

            // Act
            EndCombat();

            // Assert — despawn silencioso, sin recompensa, consumido.
            Assert.IsNull(_service.ActiveChest);
            Assert.AreEqual(chestGuid, expiredGuid);
            Assert.AreEqual(0, _economy.CurrentGold);
            Assert.IsTrue(State().Consumed);
            Assert.IsFalse(State().Opened);
            Assert.IsFalse(_grid.TryGetPosition(chestGuid, out _));
            Assert.IsNull(_attrs.GetAttribute<Health>(chestGuid));
        }

        [Test]
        public void OnCombatEnd_AfterOpen_ShouldNotFireExpired()
        {
            // Arrange
            StartCombat();
            Hit(_player.PlayerGuid, lethal: true);
            bool expired = false;
            EventManager.Subscribe(EventName.OnChestExpired, _ => expired = true);

            // Act
            EndCombat();

            // Assert
            Assert.IsFalse(expired);
        }

        // ----- stubs -----------------------------------------------------

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

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }
    }
}
