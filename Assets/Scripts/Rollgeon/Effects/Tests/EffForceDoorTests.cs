using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Effects.Concretes;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffForceDoorTests
    {
        private FakeDungeonForForce _dungeon;
        private FakePhaseServiceForForce _phase;
        private FakeGridForForce _grid;
        private EffForceDoor _effect;
        private Guid _playerGuid;
        private GameObject _spawnedRoot;

        [SetUp]
        public void SetUp()
        {
            _dungeon = new FakeDungeonForForce();
            ServiceLocator.AddService<IDungeonService>(_dungeon, ServiceScope.Run);

            _phase = new FakePhaseServiceForForce { CurrentBase = GamePhase.Combat };
            ServiceLocator.AddService<IPhaseService>(_phase, ServiceScope.Run);

            _grid = new FakeGridForForce();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Run);

            _playerGuid = Guid.NewGuid();
            _grid.PlayerCoord = new GridCoord(2, 2);
            _grid.PlayerGuid = _playerGuid;

            _effect = new EffForceDoor
            {
                RequiredValue = 10,
                EnemyHealPercentOnSuccess = 0,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnedRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_spawnedRoot);
                _spawnedRoot = null;
            }
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void DiceSum_MeetsThreshold_SetsForced_AndEntersRoom()
        {
            var instance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
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
            var instance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
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
            var instance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
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
            _dungeon.CurrentInstance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
            var ctx = MakeCtx(null);

            Assert.IsFalse(_effect.ApplyEffect(ctx));
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        [Test]
        public void EmptyDiceResult_ReturnsFalse()
        {
            _dungeon.CurrentInstance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
            var ctx = MakeCtx(Array.Empty<int>());

            Assert.IsFalse(_effect.ApplyEffect(ctx));
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        [Test]
        public void OutOfCombat_NoDice_NoEnergy_StillCrosses()
        {
            _phase.CurrentBase = GamePhase.Exploration;
            var instance = CreateInstanceWithAdjacentDoor(DoorDirection.North);
            _dungeon.CurrentInstance = instance;
            _dungeon.EnterResult = true;

            var ctx = MakeCtx(null);

            bool result = _effect.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.IsTrue(_dungeon.EnterCalled);
            instance.ObjectStates.TryGet<DoorState>(DoorDirection.North.DoorStateKey(), out var ds);
            Assert.IsTrue(ds.Forced);
        }

        [Test]
        public void NoAdjacentDoor_ReturnsFalse()
        {
            // Door en (10,10) — player en (2,2), Manhattan > 1.
            var instance = CreateInstanceWithDoorAt(DoorDirection.North, new Vector3(10f, 0f, 10f));
            _dungeon.CurrentInstance = instance;

            var ctx = MakeCtx(new int[] { 6, 6, 6, 6, 6 });

            Assert.IsFalse(_effect.ApplyEffect(ctx));
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        [Test]
        public void BuildTooltip_OutOfCombat_ReturnsNull()
        {
            _phase.CurrentBase = GamePhase.Exploration;
            Assert.IsNull(_effect.BuildTooltip());
        }

        [Test]
        public void BuildTooltip_InCombat_ContainsThresholdAndCost()
        {
            _phase.CurrentBase = GamePhase.Combat;
            _effect.EnergyCostInCombat = 2;
            _effect.RequiredValue = 25;

            var text = _effect.BuildTooltip();
            Assert.IsNotNull(text);
            StringAssert.Contains("25", text);
            StringAssert.Contains("2", text);
        }

        [Test]
        public void DiagonalDoor_NotConsideredAdjacent_ReturnsFalse()
        {
            // Door en (3,3) — player en (2,2). Manhattan = 2, NO se acepta como adyacente.
            // (Chebyshev seria 1 — el caso que antes pasaba y ahora no debe pasar.)
            var instance = CreateInstanceWithDoorAt(DoorDirection.North, new Vector3(3f, 0f, 3f));
            _dungeon.CurrentInstance = instance;

            var ctx = MakeCtx(new int[] { 6, 6, 6, 6, 6 }); // sum 30, alcanzaria el threshold

            Assert.IsFalse(_effect.ApplyEffect(ctx),
                "Una puerta en diagonal no debe contar como adyacente — Manhattan > 1.");
            Assert.IsFalse(_dungeon.EnterCalled);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private EffectContext MakeCtx(IReadOnlyList<int> dice)
        {
            return new EffectContext
            {
                SourceGuid = _playerGuid,
                DiceResult = dice,
            };
        }

        // Crea una RoomInstance con un SpawnedPrefab que contiene un DoorController
        // ubicado en la posición world (2,0,2) — adyacente al player en grid (2,2).
        private RoomInstance CreateInstanceWithAdjacentDoor(DoorDirection doorDir)
        {
            return CreateInstanceWithDoorAt(doorDir, new Vector3(2f, 0f, 2f));
        }

        private RoomInstance CreateInstanceWithDoorAt(DoorDirection doorDir, Vector3 doorWorldPos)
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

            _spawnedRoot = new GameObject($"FakeRoom_{doorDir}");
            instance.SpawnedPrefab = _spawnedRoot;

            var doorGo = new GameObject($"Door_{doorDir}");
            doorGo.transform.SetParent(_spawnedRoot.transform);
            doorGo.transform.position = doorWorldPos;
            var dc = doorGo.AddComponent<DoorController>();
            dc.Direction = doorDir;
            dc.SetState(DoorVisualState.LockedCombat);

            return instance;
        }

        // -----------------------------------------------------------------
        // Stubs
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

        private sealed class FakePhaseServiceForForce : IPhaseService
        {
            public GamePhase CurrentBase { get; set; } = GamePhase.Combat;
            public PhaseOverlay CurrentOverlay => PhaseOverlay.None;
            public void ReplacePhase(GamePhase next) => CurrentBase = next;
            public void PushOverlay(PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }

        // Stub mínimo de IGridManager — solo TryGetPosition + WorldToGrid son los
        // métodos que EffForceDoor consume. Resto throws para detectar uso accidental.
        private sealed class FakeGridForForce : IGridManager
        {
            public Guid PlayerGuid;
            public GridCoord PlayerCoord;

            public NavGraph Graph => null;
            public Vector3 GridOrigin => Vector3.zero;
            public float TileSize => 1f;

            public bool TryGetPosition(Guid entityGuid, out GridCoord coord)
            {
                if (entityGuid == PlayerGuid) { coord = PlayerCoord; return true; }
                coord = default;
                return false;
            }

            public GridCoord WorldToGrid(Vector3 world) =>
                new GridCoord(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));

            // unused
            public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f) => throw new NotImplementedException();
            public bool InBounds(GridCoord c) => throw new NotImplementedException();
            public bool IsWalkable(GridCoord c) => throw new NotImplementedException();
            public bool IsOccupied(GridCoord c) => throw new NotImplementedException();
            public bool IsFree(GridCoord c) => throw new NotImplementedException();
            public bool TryGetOccupant(GridCoord c, out Guid entityGuid) => throw new NotImplementedException();
            public void Register(Guid entityGuid, GridCoord coord) => throw new NotImplementedException();
            public void Unregister(Guid entityGuid) => throw new NotImplementedException();
            public bool Move(Guid entityGuid, GridCoord to) => throw new NotImplementedException();
            public Vector3 GridToWorld(GridCoord c) => throw new NotImplementedException();
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => throw new NotImplementedException();
        }
    }
}
