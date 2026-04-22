using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Exploration.Tests
{
    [TestFixture]
    public class ExplorationControllerTests
    {
        private StubDungeonService _stubDungeon;
        private StubPhaseService _stubPhase;
        private ExplorationController _controller;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------

        private class StubDungeonService : IDungeonService
        {
            public RoomInstance CurrentRoomInstance { get; set; }
            public RoomSO CurrentRoom => CurrentRoomInstance?.Template;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() =>
                new Dictionary<Guid, RoomInstance>();

            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() =>
                new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }

            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;

            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; private set; }
            public PhaseOverlay CurrentOverlay { get; private set; }
            public List<GamePhase> ReplacePhaseCalls { get; } = new();

            public void ReplacePhase(GamePhase next)
            {
                CurrentBase = next;
                ReplacePhaseCalls.Add(next);
            }

            public void PushOverlay(PhaseOverlay overlay) => CurrentOverlay = overlay;
            public void PopOverlay() => CurrentOverlay = PhaseOverlay.None;
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
            _stubPhase = new StubPhaseService();
            _controller = new ExplorationController(_stubDungeon, _stubPhase);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();

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

        private RoomInstance CreateInstance(string id, RoomType type,
            RoomState state = RoomState.Uncleared)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);

            return new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                State = state
            };
        }

        private void SetCurrent(RoomInstance instance)
        {
            _stubDungeon.CurrentRoomInstance = instance;
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void BeginExploration_SetsPhaseToExploration()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));

            _controller.BeginExploration();

            Assert.IsTrue(_stubPhase.ReplacePhaseCalls.Contains(GamePhase.Exploration));
        }

        [Test]
        public void BeginExploration_FiresOnExplorationStarted()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));
            bool fired = false;
            EventManager.Subscribe(EventName.OnExplorationStarted, args => fired = true);

            _controller.BeginExploration();

            Assert.IsTrue(fired);
        }

        [Test]
        public void BeginExploration_SetsIsExploringTrue()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
        }

        [Test]
        public void BeginExploration_CalledTwice_IsIdempotent()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));

            _controller.BeginExploration();
            int callsAfterFirst = _stubPhase.ReplacePhaseCalls.Count;

            _controller.BeginExploration();
            int callsAfterSecond = _stubPhase.ReplacePhaseCalls.Count;

            Assert.AreEqual(callsAfterFirst, callsAfterSecond,
                "Second call should be no-op");
        }

        [Test]
        public void BeginExploration_CombatRoom_FiresOnCombatTriggered()
        {
            SetCurrent(CreateInstance("combat_0", RoomType.Combat));
            bool fired = false;
            EventManager.Subscribe(EventName.OnCombatTriggered, args => fired = true);

            _controller.BeginExploration();

            Assert.IsTrue(fired);
        }

        [Test]
        public void BeginExploration_CombatRoom_TransitionsToCombatPhase()
        {
            SetCurrent(CreateInstance("combat_0", RoomType.Combat));

            _controller.BeginExploration();

            Assert.AreEqual(GamePhase.Combat, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_CombatRoom_SetsIsExploringFalse()
        {
            SetCurrent(CreateInstance("combat_0", RoomType.Combat));

            _controller.BeginExploration();

            Assert.IsFalse(_controller.IsExploring);
        }

        [Test]
        public void BeginExploration_BossRoom_FiresOnCombatTriggered()
        {
            SetCurrent(CreateInstance("boss_0", RoomType.Boss));
            bool fired = false;
            string receivedRoomId = null;
            EventManager.Subscribe(EventName.OnCombatTriggered, args =>
            {
                fired = true;
                receivedRoomId = (string)args[1];
            });

            _controller.BeginExploration();

            Assert.IsTrue(fired);
            Assert.AreEqual("boss_0", receivedRoomId);
        }

        [Test]
        public void BeginExploration_ShopRoom_LogsStub()
        {
            SetCurrent(CreateInstance("shop_0", RoomType.Shop, RoomState.Cleared));

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_PotionRoom_LogsStub()
        {
            SetCurrent(CreateInstance("potion_0", RoomType.Potion, RoomState.Cleared));

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_StartRoom_IsNoop()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
            Assert.AreEqual(1, _stubPhase.ReplacePhaseCalls.Count);
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.ReplacePhaseCalls[0]);
        }

        [Test]
        public void ResumeAfterCombat_RestoresExplorationPhase()
        {
            SetCurrent(CreateInstance("combat_0", RoomType.Combat));
            _controller.BeginExploration();
            Assert.AreEqual(GamePhase.Combat, _stubPhase.CurrentBase);

            _controller.ResumeAfterCombat();

            Assert.AreEqual(GamePhase.Exploration, _stubPhase.CurrentBase);
            Assert.IsTrue(_controller.IsExploring);
        }

        [Test]
        public void OnRoomEntered_ClearedCombatRoom_DoesNotRetriggerCombat()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));
            _controller.BeginExploration();

            var cleared = CreateInstance("combat_0", RoomType.Combat, RoomState.Cleared);
            SetCurrent(cleared);

            bool combatFired = false;
            EventManager.Subscribe(EventName.OnCombatTriggered, args => combatFired = true);

            EventManager.Trigger(EventName.OnRoomEntered, cleared.InstanceId, "combat_0");

            Assert.IsFalse(combatFired, "Re-entrar a sala Cleared no debe re-disparar combate");
        }

        [Test]
        public void OnRoomEntered_UnclearedCombatRoom_TriggersCombat()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));
            _controller.BeginExploration();

            var fresh = CreateInstance("combat_1", RoomType.Combat, RoomState.Uncleared);
            SetCurrent(fresh);

            bool combatFired = false;
            EventManager.Subscribe(EventName.OnCombatTriggered, args => combatFired = true);

            EventManager.Trigger(EventName.OnRoomEntered, fresh.InstanceId, "combat_1");

            Assert.IsTrue(combatFired);
            Assert.AreEqual(GamePhase.Combat, _stubPhase.CurrentBase);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));
            _controller.BeginExploration();
            _controller.Dispose();

            SetCurrent(CreateInstance("combat_0", RoomType.Combat));
            _stubPhase.ReplacePhaseCalls.Clear();

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "combat_0");

            Assert.AreEqual(0, _stubPhase.ReplacePhaseCalls.Count,
                "No phase change should happen after Dispose");
        }

        [Test]
        public void CreateAndRegister_RegistersInServiceLocator()
        {
            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IPhaseService>(_stubPhase, ServiceScope.Run);
            SetCurrent(CreateInstance("start_0", RoomType.Start, RoomState.Cleared));

            var registered = ExplorationController.CreateAndRegister();

            Assert.IsTrue(ServiceLocator.HasService<IExplorationController>());

            var resolved = ServiceLocator.GetService<IExplorationController>();
            Assert.AreSame(registered, resolved);

            registered.Dispose();
        }
    }
}
