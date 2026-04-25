using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Effects.Concretes;
using Rollgeon.GameCamera;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffForceDoorTests
    {
        private FakeDungeonForForce _dungeon;
        private EffForceDoor _effect;

        [SetUp]
        public void SetUp()
        {
            _dungeon = new FakeDungeonForForce();
            ServiceLocator.AddService<IDungeonService>(_dungeon, ServiceScope.Run);

            _effect = new EffForceDoor
            {
                Direction = DoorDirection.North,
                RequiredValue = 10,
            };
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void DiceSum_MeetsThreshold_SetsForced_AndEntersRoom()
        {
            var instance = CreateInstance(DoorDirection.North);
            _dungeon.CurrentInstance = instance;
            _dungeon.EnterResult = true;

            var ctx = MakeCtx(new int[] { 3, 3, 2, 1, 1 }); // sum = 10

            bool result = _effect.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.IsTrue(_dungeon.EnterCalled);
            Assert.AreEqual(DoorDirection.North, _dungeon.LastEnterDirection);

            instance.ObjectStates.TryGet<DoorState>(DoorDirection.North.DoorStateKey(), out var ds);
            Assert.IsTrue(ds.Forced);
        }

        [Test]
        public void DiceSum_AboveThreshold_SetsForced_AndEntersRoom()
        {
            var instance = CreateInstance(DoorDirection.North);
            _dungeon.CurrentInstance = instance;
            _dungeon.EnterResult = true;

            var ctx = MakeCtx(new int[] { 6, 6, 6, 6, 6 }); // sum = 30

            bool result = _effect.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.IsTrue(_dungeon.EnterCalled);
        }

        [Test]
        public void DiceSum_BelowThreshold_ReturnsFalse_DoesNotEnter()
        {
            var instance = CreateInstance(DoorDirection.North);
            _dungeon.CurrentInstance = instance;

            var ctx = MakeCtx(new int[] { 1, 1, 1, 1, 1 }); // sum = 5

            bool result = _effect.ApplyEffect(ctx);

            Assert.IsFalse(result);
            Assert.IsFalse(_dungeon.EnterCalled);

            instance.ObjectStates.TryGet<DoorState>(DoorDirection.North.DoorStateKey(), out var ds);
            Assert.IsFalse(ds.Forced);
        }

        [Test]
        public void NullDiceResult_ReturnsFalse()
        {
            _dungeon.CurrentInstance = CreateInstance(DoorDirection.North);
            var ctx = MakeCtx(null);

            Assert.IsFalse(_effect.ApplyEffect(ctx));
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        [Test]
        public void EmptyDiceResult_ReturnsFalse()
        {
            _dungeon.CurrentInstance = CreateInstance(DoorDirection.North);
            var ctx = MakeCtx(Array.Empty<int>());

            Assert.IsFalse(_effect.ApplyEffect(ctx));
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static EffectContext MakeCtx(IReadOnlyList<int> dice)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                DiceResult = dice,
            };
        }

        private static RoomInstance CreateInstance(DoorDirection doorDir)
        {
            var instance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                State = RoomState.Uncleared,
            };
            instance.Connections[doorDir] = Guid.NewGuid();
            instance.ObjectStates.Set(doorDir.DoorStateKey(), new DoorState
            {
                SpawnPointId = doorDir.DoorStateKey(),
                Direction = doorDir,
                Forced = false,
                Unlocked = false,
            });
            return instance;
        }

        // -----------------------------------------------------------------
        // Stub
        // -----------------------------------------------------------------

        private sealed class FakeDungeonForForce : IDungeonService
        {
            public RoomInstance CurrentInstance;
            public bool EnterResult;
            public bool EnterCalled;
            public DoorDirection? LastEnterDirection;

            public RoomSO CurrentRoom => null;
            public RoomInstance CurrentRoomInstance => CurrentInstance;
            public DoorDirection? LastEntryDirection => null;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection dir)
            {
                EnterCalled = true;
                LastEnterDirection = dir;
                return EnterResult;
            }
            public bool EnterRoomByInstanceId(Guid id) => false;
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }
    }
}
