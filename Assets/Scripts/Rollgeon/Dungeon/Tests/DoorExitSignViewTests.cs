using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Estado del cartel de salida (<see cref="DoorExitSignView"/>) según lo que
    /// <see cref="DoorController.SetState"/> propaga. El display real (flecha
    /// screen-space del <c>ExitSignIndicator</c>) queda gated por
    /// <c>Application.isPlaying</c> — acá solo se cubre la lógica de estado
    /// (<see cref="DoorExitSignView.IsShowing"/>) y la resolución del target tile.
    /// </summary>
    [TestFixture]
    public class DoorExitSignViewTests
    {
        private readonly List<Object> _createdObjects = new();

        private DoorController _controller;
        private DoorExitSignView _view;

        [SetUp]
        public void SetUp()
        {
            // Root inactivo: igual que los fixtures de DungeonManagerTests, evita que
            // Instantiate/AddComponent disparen los Awake de tooltips en EditMode.
            var root = new GameObject("DoorBossFixture");
            root.SetActive(false);
            _createdObjects.Add(root);

            _controller = root.AddComponent<DoorController>();
            _view = root.AddComponent<DoorExitSignView>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void SetState_ExitDoorOpens_ShowsIndicator()
        {
            // Arrange
            _controller.IsExit = true;

            // Act — la exit door abre cuando la sala queda Cleared (boss muerto).
            _controller.SetState(DoorVisualState.Open);

            // Assert
            Assert.IsTrue(_view.IsShowing,
                "El indicador debe marcarse visible cuando la puerta exit abre (boss derrotado).");
        }

        [Test]
        public void SetState_ExitDoorLockedDuringBossFight_KeepsIndicatorHidden()
        {
            // Arrange
            _controller.IsExit = true;

            // Act — con el boss vivo la exit door queda LockedCombat.
            _controller.SetState(DoorVisualState.LockedCombat);

            // Assert
            Assert.IsFalse(_view.IsShowing,
                "El indicador no puede aparecer antes de matar al boss.");
        }

        [Test]
        public void SetState_NonExitDoorOpens_KeepsIndicatorHidden()
        {
            // Arrange — DoorBoss usada como puerta HACIA la boss room (swap), no exit.
            _controller.IsExit = false;

            // Act
            _controller.SetState(DoorVisualState.Open);

            // Assert
            Assert.IsFalse(_view.IsShowing,
                "Una DoorBoss no-exit (puerta hacia la boss room) no debe mostrar el indicador.");
        }

        [Test]
        public void SetState_OpenThenLocked_HidesIndicator()
        {
            // Arrange
            _controller.IsExit = true;
            _controller.SetState(DoorVisualState.Open);
            Assert.IsTrue(_view.IsShowing, "Precondición: el indicador se mostró al abrir.");

            // Act — vuelta atrás (ej. resync con estado no-cleared).
            _controller.SetState(DoorVisualState.LockedCombat);

            // Assert
            Assert.IsFalse(_view.IsShowing,
                "El indicador debe ocultarse si la puerta deja de estar abierta.");
        }

        [Test]
        public void ResolveFrontTileCenter_NorthDoor_ReturnsInnerTileCenter()
        {
            // Arrange — grid con origin/tileSize no triviales para cubrir la
            // conversión completa. Puerta North sobre el borde: coord (3,5).
            var grid = new CenterMathGridStub(new Vector3(10f, 0f, 20f), 2f);
            var doorWorldPos = new Vector3(10f + 3.2f * 2f, 0f, 20f + 5.7f * 2f);

            // Act — North entra hacia (0,-1): casilla interior (3,4).
            var center = DoorExitSignView.ResolveFrontTileCenter(grid, doorWorldPos, DoorDirection.North);

            // Assert — centro de (3,4): origin + ((3+0.5)*2, 0, (4+0.5)*2).
            Assert.AreEqual(new Vector3(17f, 0f, 29f), center,
                "El target debe ser el centro de la primera casilla interior frente a la puerta.");
        }

        /// <summary>
        /// Stub con la MISMA aritmética centro-de-celda que <c>GridManager</c>
        /// (GridToWorld = centro; WorldToGrid = floor) — lo único que
        /// <see cref="DoorExitSignView.ResolveFrontTileCenter"/> consume.
        /// </summary>
        private sealed class CenterMathGridStub : IGridManager
        {
            public CenterMathGridStub(Vector3 origin, float tileSize)
            {
                GridOrigin = origin;
                TileSize = tileSize;
            }

            public NavGraph Graph { get; } = new NavGraph();
            public Vector3 GridOrigin { get; }
            public float TileSize { get; }

            public Vector3 GridToWorld(GridCoord c) =>
                GridOrigin + new Vector3((c.X + 0.5f) * TileSize, 0f, (c.Y + 0.5f) * TileSize);

            public GridCoord WorldToGrid(Vector3 world)
            {
                var local = world - GridOrigin;
                return new GridCoord(
                    Mathf.FloorToInt(local.x / TileSize),
                    Mathf.FloorToInt(local.z / TileSize));
            }

            public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f) { }
            public bool InBounds(GridCoord c) => true;
            public bool IsWalkable(GridCoord c) => true;
            public bool IsOccupied(GridCoord c) => false;
            public bool IsFree(GridCoord c) => true;

            public bool TryGetOccupant(GridCoord c, out Guid entityGuid)
            {
                entityGuid = Guid.Empty;
                return false;
            }

            public bool TryGetPosition(Guid entityGuid, out GridCoord coord)
            {
                coord = default;
                return false;
            }

            public void Register(Guid entityGuid, GridCoord coord) { }
            public void Unregister(Guid entityGuid) { }
            public bool Move(Guid entityGuid, GridCoord to) => false;
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() =>
                Array.Empty<KeyValuePair<Guid, GridCoord>>();
        }
    }
}
