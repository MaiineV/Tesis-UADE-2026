using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    /// <summary>
    /// BUG-069: <see cref="ChestService.TryPickFreeCoord"/> no debe elegir un nodo
    /// walkable-pero-aislado (grado 0 en el NavGraph) — quedaba spawneando cofres
    /// atrapados entre pared y assets, en celdas islas dejadas por el bake viejo.
    /// </summary>
    [TestFixture]
    public sealed class ChestConnectivityFilterTests
    {
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        private ChestConfigSO _config;
        private ChestLootPoolSO _pool;
        private ChestService _service;
        private AttributesManager _attrs;
        private GridManager _grid;
        private StubDungeon _dungeon;
        private RoomInstance _room;
        private RoomSO _combatRoom;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _grid = new GridManager();
            ServiceLocator.AddService<IGridManager>(_grid);

            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = Guid.NewGuid() });
            ServiceLocator.AddService<Rollgeon.Items.IInventoryService>(new FakeInventoryService());
            ServiceLocator.AddService<Rollgeon.Economy.IEconomyService>(new FakeEconomyService());

            _combatRoom = ScriptableObject.CreateInstance<RoomSO>();
            _combatRoom.Type = RoomType.Combat;
            _assets.Add(_combatRoom);

            _room = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = _combatRoom,
                GridCell = new Vector2Int(0, 0),
                State = RoomState.Uncleared,
            };
            _dungeon = new StubDungeon { Room = _room, FloorSeed = 1 };
            ServiceLocator.AddService<IDungeonService>(_dungeon);

            _config = ScriptableObject.CreateInstance<ChestConfigSO>();
            foreach (ItemRarity tier in Enum.GetValues(typeof(ItemRarity)))
                _config.Tiers.Add(new ChestTierDef { Tier = tier, MaxHP = 20, FallbackGold = 5 });
            _assets.Add(_config);

            _pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            foreach (ItemRarity tier in Enum.GetValues(typeof(ItemRarity)))
                _pool.Buckets.Add(new ChestLootBucket { Tier = tier, GoldMin = 1, GoldMax = 1 });
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
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void DebugSpawn_ShouldFail_WhenOnlyCandidateIsIsolatedNode()
        {
            // Arrange — un único nodo walkable en todo el grafo, sin edges (isla).
            var graph = new NavGraph();
            graph.AddNode(new NavNode(new GridCoord(0, 0)));
            _grid.LoadRoom(graph);

            // Act — sin tiles libres CONECTADOS, el spawn debe abortar (no elegir la isla).
            bool spawned = _service.DebugSpawn(ItemRarity.Common, isMimic: false);

            // Assert
            Assert.IsFalse(spawned);
            Assert.IsNull(_service.ActiveChest);
        }

        [Test]
        public void DebugSpawn_ShouldExcludeIsolatedNode_WhenAConnectedAlternativeExists()
        {
            // Arrange — (0,0) aislado (grado 0); (5,5) es el ÚNICO nodo conectado (tiene un
            // vecino real). Con un solo candidato válido, el resultado es determinista: si el
            // filtro de conectividad funciona, SIEMPRE cae en (5,5) — nunca en la isla.
            var graph = new NavGraph();
            graph.AddNode(new NavNode(new GridCoord(0, 0))); // isla, sin edges
            graph.AddNode(new NavNode(new GridCoord(5, 5)));
            graph.AddNode(new NavNode(new GridCoord(6, 5)));
            graph.AddBidirectionalEdge(new GridCoord(5, 5), new GridCoord(6, 5));
            _grid.LoadRoom(graph);
            _grid.Register(Guid.NewGuid(), new GridCoord(6, 5)); // ocupado: único candidato libre = (5,5)

            // Act
            bool spawned = _service.DebugSpawn(ItemRarity.Common, isMimic: false);

            // Assert
            Assert.IsTrue(spawned);
            Assert.AreEqual(new GridCoord(5, 5), _service.ActiveChest.Coord);
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
            public Heroes.ClassHeroSO CurrentHero => null;
            public Dice.DiceBagSO DiceBag => null;
            public void SetPlayer(Heroes.ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Dice.DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable 67
            public event Action<Heroes.ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }
    }
}
