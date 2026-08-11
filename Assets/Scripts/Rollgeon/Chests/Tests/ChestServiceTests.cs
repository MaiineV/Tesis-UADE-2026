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

        // ----- mimic -----------------------------------------------------

        private Entities.EnemyDataSO _mimicData;

        private void ArrangeMimic(float statScale = 0.5f)
        {
            _mimicData = ScriptableObject.CreateInstance<Entities.EnemyDataSO>();
            _mimicData.EntityId = "mimic.test";
            _mimicData.BaseHP = 60;
            _mimicData.BaseAttack = 20;
            _mimicData.BaseSpeed = 4;
            _assets.Add(_mimicData);
            _config.MimicEnemy = _mimicData;
            foreach (var tier in _config.Tiers)
            {
                tier.MimicStatScale = statScale;
                tier.MimicGoldMin = 7;
                tier.MimicGoldMax = 7;
            }

            ServiceLocator.AddService<Combat.Initiative.InMemoryEntityRegistry>(
                new Combat.Initiative.InMemoryEntityRegistry());
            ServiceLocator.AddService<Combat.TurnOrderService>(new Combat.TurnOrderService());

            Assert.IsTrue(_service.DebugSpawn(ItemRarity.Common, isMimic: true));
        }

        [Test]
        public void MimicAnyPlayerHit_ShouldActivateMeleeAtChestTile()
        {
            // Arrange
            ArrangeMimic(statScale: 0.5f);
            var chestGuid = _service.ActiveChest.Guid;
            var chestCoord = _service.ActiveChest.Coord;
            Guid activatedChest = Guid.Empty, mimicGuid = Guid.Empty;
            EventManager.Subscribe(EventName.OnChestMimicActivated, args =>
            {
                activatedChest = (Guid)args[0];
                mimicGuid = (Guid)args[1];
            });
            bool reinforcementAnnounced = false;
            EventManager.Subscribe(EventName.OnReinforcementSpawned, _ => reinforcementAnnounced = true);

            // Act — golpe NO letal del jugador: igual activa (GDD §28, confirmado).
            Hit(_player.PlayerGuid, lethal: false);

            // Assert — cofre fuera, Melee escalado en su tile, en la cola y contando
            // para el clear, con el aviso de refuerzo (sin turno sorpresa).
            Assert.AreEqual(chestGuid, activatedChest);
            Assert.AreNotEqual(Guid.Empty, mimicGuid);
            Assert.IsFalse(_service.IsChest(chestGuid));
            Assert.IsNull(_attrs.GetAttribute<Health>(chestGuid));
            Assert.AreEqual(30, _attrs.GetAttribute<Health>(mimicGuid).Value); // 60 × 0.5
            Assert.IsTrue(_grid.TryGetPosition(mimicGuid, out var mimicCoord));
            Assert.AreEqual(chestCoord, mimicCoord);
            Assert.IsTrue(reinforcementAnnounced);
            CollectionAssert.Contains(_room.SpawnedEnemies, mimicGuid);
            Assert.AreEqual(ChestPhase.MimicActive, _service.ActiveChest.Phase);
        }

        [Test]
        public void MimicEnemyHit_ShouldNotActivateNorBreak()
        {
            // Arrange
            ArrangeMimic();
            var chestGuid = _service.ActiveChest.Guid;

            // Act — golpe "letal" de un enemigo (en runtime el clamp del pipeline lo
            // impide; acá validamos que el service tampoco resuelve nada).
            Hit(Guid.NewGuid(), lethal: true);

            // Assert — el Mimic sigue esperando el contacto del jugador.
            Assert.IsTrue(_service.IsChest(chestGuid));
            Assert.AreEqual(ChestPhase.Idle, _service.ActiveChest.Phase);
            CollectionAssert.IsEmpty(_room.SpawnedEnemies);
        }

        [Test]
        public void TryGetMinHp_ShouldClampOnlyUnactivatedMimic_AgainstNonPlayerSources()
        {
            // Arrange
            ArrangeMimic();
            var chestGuid = _service.ActiveChest.Guid;

            // Act + Assert — enemigo: clampea a 1; jugador: no; otro guid: no.
            Assert.IsTrue(_service.TryGetMinHp(chestGuid, Guid.NewGuid(), out int minHp));
            Assert.AreEqual(1, minHp);
            Assert.IsFalse(_service.TryGetMinHp(chestGuid, _player.PlayerGuid, out _));
            Assert.IsFalse(_service.TryGetMinHp(Guid.NewGuid(), Guid.NewGuid(), out _));
        }

        [Test]
        public void TryGetMinHp_ShouldNotClampNormalChest()
        {
            // Arrange — cofre normal (no mimic).
            StartCombat();
            var chestGuid = _service.ActiveChest.Guid;

            // Act + Assert
            Assert.IsFalse(_service.TryGetMinHp(chestGuid, Guid.NewGuid(), out _));
        }

        [Test]
        public void MimicDeath_ShouldPayTierGold()
        {
            // Arrange — EnemyGoldDropService real escuchando OnEntityDestroyed.
            using var goldDrops = new Rollgeon.Economy.EnemyGoldDropService(_economy);
            ServiceLocator.AddService<Rollgeon.Economy.EnemyGoldDropService>(goldDrops);
            ArrangeMimic();
            Hit(_player.PlayerGuid, lethal: false);
            var mimicGuid = _service.ActiveChest.MimicEnemyGuid;

            // Act — muere el Mimic (camino normal de CombatDeathWatcher).
            EventManager.Trigger(EventName.OnEntityDestroyed, mimicGuid, _player.PlayerGuid);

            // Assert — solo oro, monto del tier (7).
            Assert.AreEqual(7, _economy.CurrentGold);
            Assert.AreEqual(0, _inventory.Added.Count);
        }

        [Test]
        public void OnCombatEnd_WithLiveMimic_ShouldRemoveItFromSpawnedEnemies()
        {
            // Arrange — escape con el Mimic activado y vivo: su guid no persiste, no
            // puede quedar colgado en SpawnedEnemies para el re-entry.
            ArrangeMimic();
            Hit(_player.PlayerGuid, lethal: false);
            var mimicGuid = _service.ActiveChest.MimicEnemyGuid;
            CollectionAssert.Contains(_room.SpawnedEnemies, mimicGuid);

            // Act
            EndCombat();

            // Assert — sin expiración espuria (ya estaba resuelto como MimicActive).
            CollectionAssert.DoesNotContain(_room.SpawnedEnemies, mimicGuid);
            Assert.IsNull(_service.ActiveChest);
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
