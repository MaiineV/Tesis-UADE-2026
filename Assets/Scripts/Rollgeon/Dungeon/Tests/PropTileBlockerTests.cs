using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Bloqueo de celda bajo props runtime (<see cref="PropTileBlocker"/>): el
    /// altar/pedestal registra su celda en el <see cref="IGridManager"/> solo si
    /// vive en la sala activa y la celda está libre, y la libera al destruirse.
    /// TryRegister/TryUnregister se invocan por reflection — el diferimiento por
    /// coroutine no corre en EditMode.
    /// </summary>
    [TestFixture]
    public class PropTileBlockerTests
    {
        private FakeDungeonService _dungeon;
        private GridManager _grid;
        private GameObject _roomRoot;
        private PropTileBlocker _blocker;
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(4, 4));
            ServiceLocator.AddService<IGridManager>(_grid);

            _roomRoot = new GameObject("RoomPrefab_active");
            _roomRoot.SetActive(false);
            _spawned.Add(_roomRoot);

            _dungeon = new FakeDungeonService
            {
                CurrentInstance = new RoomInstance
                {
                    InstanceId = Guid.NewGuid(),
                    SpawnedPrefab = _roomRoot,
                },
            };
            ServiceLocator.AddService<IDungeonService>(_dungeon);

            // Prop en la celda (1,1) de la sala activa. GO inactivo: OnEnable no
            // corre en el rig y el registro se dispara a mano via reflection.
            var propGo = new GameObject("Prop");
            propGo.transform.SetParent(_roomRoot.transform, false);
            propGo.transform.position = _grid.GridToWorld(new GridCoord(1, 1));
            _blocker = propGo.AddComponent<PropTileBlocker>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                if (obj != null) Object.DestroyImmediate(obj);
            _spawned.Clear();

            ServiceLocator.RemoveService<IGridManager>();
            ServiceLocator.RemoveService<IDungeonService>();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void TryRegister_PropInActiveRoom_OccupiesItsCell()
        {
            Invoke(_blocker, "TryRegister");

            Assert.IsTrue(_grid.IsOccupied(new GridCoord(1, 1)),
                "La celda bajo el prop debe quedar ocupada (intransitable).");
        }

        [Test]
        public void TryRegister_CellAlreadyOccupied_DoesNotOverride()
        {
            var playerGuid = Guid.NewGuid();
            _grid.Register(playerGuid, new GridCoord(1, 1));

            Invoke(_blocker, "TryRegister");

            Assert.IsTrue(_grid.TryGetOccupant(new GridCoord(1, 1), out var occupant));
            Assert.AreEqual(playerGuid, occupant,
                "Con la celda tomada (ej. el jugador parado encima) el blocker no pisa.");
        }

        [Test]
        public void TryRegister_PropInAnotherRoom_DoesNotRegister()
        {
            var otherRoomRoot = new GameObject("RoomPrefab_other");
            otherRoomRoot.SetActive(false);
            _spawned.Add(otherRoomRoot);
            _blocker.transform.SetParent(otherRoomRoot.transform, worldPositionStays: true);

            Invoke(_blocker, "TryRegister");

            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)),
                "Un prop de una sala no activa no debe bloquear celdas de la grilla actual.");
        }

        [Test]
        public void TryUnregister_AfterRegister_FreesTheCell()
        {
            Invoke(_blocker, "TryRegister");

            Invoke(_blocker, "TryUnregister");

            Assert.IsTrue(_grid.IsFree(new GridCoord(1, 1)),
                "Al destruirse el prop (ej. pedestal comprado) la celda vuelve a ser transitable.");
        }

        [Test]
        public void TryRegister_PropWithMultiTileVisual_BlocksWholeFootprintAndFreesIt()
        {
            // Mesa estilo altar: visual de 4x5 tiles. Grid más grande y room activa
            // (Renderer.bounds solo es confiable con el GO activo).
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            _roomRoot.SetActive(true);
            _blocker.transform.position = new Vector3(3f, 0f, 2.5f);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(_blocker.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(4f, 1f, 5f);

            Invoke(_blocker, "TryRegister");

            // Bounds XZ = [1,5]x[0,5] → centros de celda adentro: x 1..4, y 0..4.
            for (int x = 1; x <= 4; x++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    Assert.IsTrue(_grid.IsOccupied(new GridCoord(x, y)),
                        $"La celda ({x},{y}) bajo la mesa debe quedar bloqueada.");
                }
            }
            Assert.IsTrue(_grid.IsFree(new GridCoord(0, 0)),
                "Las celdas fuera del footprint siguen libres.");
            Assert.IsTrue(_grid.IsFree(new GridCoord(5, 5)));

            Invoke(_blocker, "TryUnregister");

            Assert.IsTrue(_grid.IsFree(new GridCoord(2, 2)),
                "TryUnregister debe liberar TODAS las celdas del footprint.");
            Assert.IsTrue(_grid.IsFree(new GridCoord(4, 4)));
        }

        [Test]
        public void TryRegister_AfterLoadRoomClearedOccupancy_ReblocksTheCell()
        {
            Invoke(_blocker, "TryRegister");

            // Re-entrada a la sala: LoadRoom limpia toda la ocupancia.
            _grid.LoadRoom(NavGraph.Rect(4, 4));
            Assert.IsFalse(_grid.IsOccupied(new GridCoord(1, 1)));

            Invoke(_blocker, "TryRegister");

            Assert.IsTrue(_grid.IsOccupied(new GridCoord(1, 1)),
                "El blocker debe re-registrar tras cada carga de sala.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Método '{methodName}' no encontrado en {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        // -----------------------------------------------------------------
        // Stub service (mismo shape que el de RoomGridLoaderTests)
        // -----------------------------------------------------------------

        private sealed class FakeDungeonService : IDungeonService
        {
            public RoomInstance CurrentInstance;

            public RoomSO CurrentRoom => CurrentInstance?.Template;
            public RoomInstance CurrentRoomInstance => CurrentInstance;

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

            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public bool SetRoomState(Guid id, RoomState state) => false;
            public void ResyncDoorVisuals(Guid id) { }

            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }
    }
}
