using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
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
            public RoomSO CurrentRoom { get; set; }
            public int CurrentRoomIndex { get; set; }
            public int RoomCount { get; set; }
            public bool IsLastRoom { get; set; }
            public bool NextRoomReturnValue { get; set; } = true;
            public int NextRoomCallCount { get; private set; }

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public bool NextRoom()
            {
                NextRoomCallCount++;
                return NextRoomReturnValue;
            }

            public IReadOnlyList<RoomSO> GetFloorRooms() => Array.Empty<RoomSO>();
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

        private RoomSO CreateRoom(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void BeginExploration_SetsPhaseToExploration()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);

            _controller.BeginExploration();

            Assert.IsTrue(_stubPhase.ReplacePhaseCalls.Contains(GamePhase.Exploration));
        }

        [Test]
        public void BeginExploration_FiresOnExplorationStarted()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            bool fired = false;
            EventManager.Subscribe(EventName.OnExplorationStarted, args => fired = true);

            _controller.BeginExploration();

            Assert.IsTrue(fired);
        }

        [Test]
        public void BeginExploration_SetsIsExploringTrue()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
        }

        [Test]
        public void BeginExploration_CalledTwice_IsIdempotent()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);

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
            _stubDungeon.CurrentRoom = CreateRoom("combat_0", RoomType.Combat);
            bool fired = false;
            EventManager.Subscribe(EventName.OnCombatTriggered, args => fired = true);

            _controller.BeginExploration();

            Assert.IsTrue(fired);
        }

        [Test]
        public void BeginExploration_CombatRoom_TransitionsToCombatPhase()
        {
            _stubDungeon.CurrentRoom = CreateRoom("combat_0", RoomType.Combat);

            _controller.BeginExploration();

            Assert.AreEqual(GamePhase.Combat, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_CombatRoom_SetsIsExploringFalse()
        {
            _stubDungeon.CurrentRoom = CreateRoom("combat_0", RoomType.Combat);

            _controller.BeginExploration();

            Assert.IsFalse(_controller.IsExploring);
        }

        [Test]
        public void BeginExploration_BossRoom_FiresOnCombatTriggered()
        {
            _stubDungeon.CurrentRoom = CreateRoom("boss_0", RoomType.Boss);
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
            _stubDungeon.CurrentRoom = CreateRoom("shop_0", RoomType.Shop);

            _controller.BeginExploration();

            // Shop is a stub — no phase change, exploration continues
            Assert.IsTrue(_controller.IsExploring);
            // Phase should still be Exploration (set in BeginExploration), not Combat
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_PotionRoom_LogsStub()
        {
            _stubDungeon.CurrentRoom = CreateRoom("potion_0", RoomType.Potion);

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.CurrentBase);
        }

        [Test]
        public void BeginExploration_StartRoom_IsNoop()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);

            _controller.BeginExploration();

            Assert.IsTrue(_controller.IsExploring);
            // Only the Exploration phase call, no Combat transition
            Assert.AreEqual(1, _stubPhase.ReplacePhaseCalls.Count);
            Assert.AreEqual(GamePhase.Exploration, _stubPhase.ReplacePhaseCalls[0]);
        }

        [Test]
        public void AdvanceRoom_DelegatesToDungeonNextRoom()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            _controller.BeginExploration();

            _stubDungeon.CurrentRoom = CreateRoom("combat_next", RoomType.Start);
            _controller.AdvanceRoom();

            Assert.AreEqual(1, _stubDungeon.NextRoomCallCount);
        }

        [Test]
        public void AdvanceRoom_ReturnsTrueWhenAdvanced()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            _controller.BeginExploration();
            _stubDungeon.NextRoomReturnValue = true;

            bool result = _controller.AdvanceRoom();

            Assert.IsTrue(result);
        }

        [Test]
        public void AdvanceRoom_ReturnsFalseWhenFloorCleared()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            _controller.BeginExploration();
            _stubDungeon.NextRoomReturnValue = false;

            bool result = _controller.AdvanceRoom();

            Assert.IsFalse(result);
        }

        [Test]
        public void AdvanceRoom_WhenNotExploring_ReturnsFalse()
        {
            Assert.IsFalse(_controller.IsExploring);

            bool result = _controller.AdvanceRoom();

            Assert.IsFalse(result);
            Assert.AreEqual(0, _stubDungeon.NextRoomCallCount,
                "Should not delegate to dungeon when not exploring");
        }

        [Test]
        public void AdvanceRoom_CombatRoomNext_TriggersPhaseTransition()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            _controller.BeginExploration();

            // NextRoom fires OnRoomEntered which ProcessRoom picks up
            _stubDungeon.CurrentRoom = CreateRoom("combat_1", RoomType.Combat);
            _stubDungeon.NextRoomReturnValue = true;

            bool combatTriggered = false;
            EventManager.Subscribe(EventName.OnCombatTriggered, args => combatTriggered = true);

            _controller.AdvanceRoom();
            // OnRoomEntered is fired by DungeonManager.NextRoom in production;
            // since we use a stub, simulate the event
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "combat_1");

            Assert.IsTrue(combatTriggered);
            Assert.AreEqual(GamePhase.Combat, _stubPhase.CurrentBase);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);
            _controller.BeginExploration();
            _controller.Dispose();

            // After dispose, triggering OnRoomEntered should NOT call ProcessRoom
            _stubDungeon.CurrentRoom = CreateRoom("combat_0", RoomType.Combat);
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
            _stubDungeon.CurrentRoom = CreateRoom("start_0", RoomType.Start);

            var registered = ExplorationController.CreateAndRegister();

            Assert.IsTrue(ServiceLocator.HasService<IExplorationController>());

            var resolved = ServiceLocator.GetService<IExplorationController>();
            Assert.AreSame(registered, resolved);

            registered.Dispose();
        }
    }
}
