using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Entities;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    [TestFixture]
    public class CombatHandoffServiceTests
    {
        private StubDungeonService _stubDungeon;
        private StubPlayerService _stubPlayer;
        private SpyEnemySpawnResolver _spyResolver;
        private SpyEnemyAIHandler _spyAI;
        private SpyScreenManager _spyScreen;
        private SpyCombatStarter _spyCombat;
        private CombatHandoffService _service;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Stubs / Spies
        // -------------------------------------------------------------------

        private class StubDungeonService : IDungeonService
        {
            public RoomSO CurrentRoom { get; set; }
            public RoomInstance CurrentRoomInstance { get; set; }

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() =>
                new Dictionary<Guid, RoomInstance>();

            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() =>
                new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(Rollgeon.Dungeon.Components.DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }

            public Rollgeon.Dungeon.Components.DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(Rollgeon.Dungeon.Components.DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;

            public UnityEngine.Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        private class SpyEnemySpawnResolver : IEnemySpawnResolver
        {
            public int ResolveCallCount { get; private set; }
            public RoomSO LastRoom { get; private set; }
            public RoomInstance LastInstance { get; private set; }
            public List<(Guid id, EnemyDataSO data)> ReturnValue { get; set; } = new();

            public List<(Guid id, EnemyDataSO data)> Resolve(RoomInstance instance, System.Random rng)
            {
                ResolveCallCount++;
                LastInstance = instance;
                LastRoom = instance?.Template;
                return ReturnValue;
            }
        }

        private class SpyEnemyAIHandler : IEnemyAIHandler
        {
            public int HandleCallCount { get; private set; }
            public Guid LastEnemyId { get; private set; }

            public void HandleEnemyTurn(Guid enemyId)
            {
                HandleCallCount++;
                LastEnemyId = enemyId;
            }
        }

        private class SpyScreenManager : IScreenManager
        {
            public IBaseScreen Current { get; private set; }
            public int PushByStringIdCallCount { get; private set; }
            public string LastScreenId { get; private set; }
            public IScreenPayload LastPayload { get; private set; }

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null)
            {
                PushByStringIdCallCount++;
                LastScreenId = screenId;
                LastPayload = payload;
            }
            public void PopCurrent() { }
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private class SpyCombatStarter : ICombatStarter
        {
            public int StartCombatCallCount { get; private set; }
            public Guid LastPlayerId { get; private set; }
            public IReadOnlyList<Guid> LastParticipants { get; private set; }
            public Guid LastRoomInstanceId { get; private set; }
            public Action<Guid> LastEnemyActionHandler { get; private set; }

            public void StartCombat(
                Guid playerId,
                IReadOnlyList<Guid> participants,
                Guid roomInstanceId,
                Action<Guid> enemyActionHandler)
            {
                StartCombatCallCount++;
                LastPlayerId = playerId;
                LastParticipants = participants;
                LastRoomInstanceId = roomInstanceId;
                LastEnemyActionHandler = enemyActionHandler;
            }
        }

        private class StubPlayerCombatActions : IPlayerCombatActions
        {
            public void SendPlayerAction() { }
            public void EndPlayerTurn() { }
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _stubDungeon = new StubDungeonService();
            _stubPlayer = new StubPlayerService();
            _spyResolver = new SpyEnemySpawnResolver();
            _spyAI = new SpyEnemyAIHandler();
            _spyScreen = new SpyScreenManager();
            _spyCombat = new SpyCombatStarter();

            _service = new CombatHandoffService(
                _stubDungeon, _stubPlayer, _spyResolver,
                _spyAI, _spyScreen, _spyCombat, new StubPlayerCombatActions());
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RoomSO CreateRoom(RoomType type, string id = "test_room")
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        private void SetCurrentRoom(RoomSO room)
        {
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
            };
        }

        private EnemyDataSO CreateEnemy(string name)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            _createdObjects.Add(enemy);
            return enemy;
        }

        private void TriggerCombat(Guid roomInstanceId, string roomId, RoomType roomType)
        {
            EventManager.Trigger(EventName.OnCombatTriggered, roomInstanceId, roomId, roomType);
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatTriggered_CallsResolverWithCurrentRoom()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (Guid.NewGuid(), enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyResolver.ResolveCallCount);
            Assert.AreSame(room, _spyResolver.LastRoom);
        }

        [Test]
        public void OnCombatTriggered_PushesCombatHUDScreen()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyScreen.PushByStringIdCallCount);
            Assert.AreEqual("CombatHUD", _spyScreen.LastScreenId);
        }

        [Test]
        public void OnCombatTriggered_CallsStartCombat()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            var roomInstanceId = Guid.NewGuid();
            TriggerCombat(roomInstanceId, "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyCombat.StartCombatCallCount);
            Assert.AreEqual(roomInstanceId, _spyCombat.LastRoomInstanceId);
        }

        [Test]
        public void OnCombatTriggered_ParticipantsIncludePlayer()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemyId = Guid.NewGuid();
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (enemyId, enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsNotNull(_spyCombat.LastParticipants);
            Assert.IsTrue(
                ((List<Guid>)_spyCombat.LastParticipants).Contains(_stubPlayer.PlayerGuid),
                "Participants must include the player Guid");
        }

        [Test]
        public void OnCombatTriggered_ParticipantsIncludeEnemies()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemyId = Guid.NewGuid();
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (enemyId, enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsTrue(
                ((List<Guid>)_spyCombat.LastParticipants).Contains(enemyId),
                "Participants must include enemy Guids");
        }

        [Test]
        public void OnCombatTriggered_BossRoom_SpawnsOneEnemy()
        {
            var room = CreateRoom(RoomType.Boss);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "boss_room", RoomType.Boss);

            Assert.AreEqual(RoomType.Boss, _spyResolver.LastInstance?.Template?.Type,
                "Boss rooms should pass the boss instance to the resolver.");
        }

        [Test]
        public void OnCombatTriggered_CombatRoom_PassesCombatInstance()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "combat_room", RoomType.Combat);

            Assert.AreEqual(RoomType.Combat, _spyResolver.LastInstance?.Template?.Type,
                "Combat rooms should pass the combat instance to the resolver.");
        }

        [Test]
        public void OnCombatTriggered_PassesEnemyAIHandlerToStartCombat()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsNotNull(_spyCombat.LastEnemyActionHandler);
            // Invoke to verify it routes to the AI handler.
            var testId = Guid.NewGuid();
            _spyCombat.LastEnemyActionHandler(testId);
            Assert.AreEqual(1, _spyAI.HandleCallCount);
            Assert.AreEqual(testId, _spyAI.LastEnemyId);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvent()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            _service.Dispose();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(0, _spyResolver.ResolveCallCount,
                "After Dispose, resolver should not be called");
        }

        [Test]
        public void OnCombatTriggered_NullCurrentRoom_DoesNotCallStartCombat()
        {
            _stubDungeon.CurrentRoom = null;

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(0, _spyCombat.StartCombatCallCount,
                "StartCombat should not be called when current room is null");
        }
    }
}
