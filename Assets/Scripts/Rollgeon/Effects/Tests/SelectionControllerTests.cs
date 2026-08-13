using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public sealed class SelectionControllerTests
    {
        // El highlight ahora tintea via el contrato _HitFlash* del shader del piso
        // (TileHighlightService.ApplyStyle) — el color del estilo va a _HitFlashColor.
        private static readonly int ColorId = Shader.PropertyToID("_HitFlashColor");

        private static readonly Color PathColor = new Color(0.45f, 1f, 0.55f, 0.85f);
        private static readonly Color DoorColor = new Color(1f, 0f, 0f, 0.7f);
        private static readonly Color MoveColor = new Color(0.3f, 0.8f, 1f, 0.28f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private TileHighlightService _highlight;
        private StubMovementService _movement;
        private SelectionController _controller;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _owner = Guid.NewGuid();

            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(4, 1));
            grid.Register(_owner, new GridCoord(0, 0));
            ServiceLocator.AddService<IGridManager>(grid, ServiceScope.Global);

            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _highlight = new TileHighlightService();
            ServiceLocator.AddService<ITileHighlightService>(_highlight, ServiceScope.Global);

            _controller = new SelectionController();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            ServiceLocator.Clear();
        }

        private Renderer RegisterTileRenderer(int x, int z)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(x, 0f, z);
            _objects.Add(cube);
            var renderer = cube.GetComponent<Renderer>();
            _highlight.RegisterTile(new GridCoord(x, z), renderer);
            return renderer;
        }

        private static Color PaintedColor(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(ColorId);
        }

        // El MPB no devuelve los floats bit-idénticos — comparar por canal
        // con tolerancia en vez de Color.Equals exacto.
        private static void AssertColorApprox(Color expected, Color actual, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-3f), message + " (a)");
        }

        private void BeginMoveSelectionWithDoor()
        {
            _controller.BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                HighlightStyle = "move",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(1, 0)) },
                DoorTiles = new HashSet<GridCoord> { new GridCoord(3, 0) },
            });
        }

        [Test]
        public void OnTargetHovered_ValidMoveTile_PaintsPathPreview()
        {
            // Arrange
            RegisterTileRenderer(0, 0);
            var destination = RegisterTileRenderer(1, 0);
            _movement.PathToReturn = new List<GridCoord>
            {
                new GridCoord(0, 0), new GridCoord(1, 0),
            };
            BeginMoveSelectionWithDoor();

            // Act
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(1, 0)));

            // Assert
            AssertColorApprox(PathColor, PaintedColor(destination),
                "Hovear un tile válido de move debe pintar el camino A*.");
        }

        [Test]
        public void OnTargetHovered_DoorTile_PaintsPathAndKeepsDoorRed()
        {
            // Arrange — el héroe camina hasta la casilla frente a puerta antes de
            // cruzar, así que el hover también debe previewar el camino (BUG).
            RegisterTileRenderer(0, 0);
            RegisterTileRenderer(1, 0);
            var middle = RegisterTileRenderer(2, 0);
            var door = RegisterTileRenderer(3, 0);
            _movement.PathToReturn = new List<GridCoord>
            {
                new GridCoord(0, 0), new GridCoord(1, 0),
                new GridCoord(2, 0), new GridCoord(3, 0),
            };
            BeginMoveSelectionWithDoor();

            // Act
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(3, 0)));

            // Assert
            AssertColorApprox(PathColor, PaintedColor(middle),
                "El tile intermedio del camino a la puerta debe pintarse como path.");
            AssertColorApprox(DoorColor, PaintedColor(door),
                "La casilla frente a puerta debe seguir roja por encima del path.");
        }

        [Test]
        public void CanOverlayHoverPreview_NoActiveSelection_IsTrue()
        {
            // Arrange — sin BeginSelection.

            // Act + Assert
            Assert.IsTrue(_controller.CanOverlayHoverPreview,
                "Sin selección activa el hover del HUD puede pintar libremente.");
        }

        [Test]
        public void CanOverlayHoverPreview_ActiveSelectionWithVisibleRange_IsFalse()
        {
            // Arrange
            BeginMoveSelectionWithDoor();

            // Act + Assert
            Assert.IsFalse(_controller.CanOverlayHoverPreview,
                "Con un rango de selección visible el hover del HUD no debe pisarlo.");
        }

        [Test]
        public void CanOverlayHoverPreview_SuppressedRangeSelection_IsTrue()
        {
            // Arrange — movimiento de Exploración: armado permanente sin rango pintado.
            _controller.BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                HighlightStyle = "move",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(1, 0)) },
                SuppressRangeHighlight = true,
            });

            // Act + Assert
            Assert.IsTrue(_controller.CanOverlayHoverPreview,
                "Con rango suprimido (Exploración) el hover del HUD sí puede pintar.");
        }

        [Test]
        public void RefreshHighlights_ActiveSelection_RepaintsRangeAndDoors()
        {
            // Arrange — un ClearAll ajeno (hover del HUD al salir) borra todo el tinte.
            var rangeTile = RegisterTileRenderer(1, 0);
            var doorTile = RegisterTileRenderer(3, 0);
            BeginMoveSelectionWithDoor();
            _highlight.ClearAll();

            // Act
            _controller.RefreshHighlights();

            // Assert
            AssertColorApprox(MoveColor, PaintedColor(rangeTile),
                "RefreshHighlights debe repintar el rango de la selección activa.");
            AssertColorApprox(DoorColor, PaintedColor(doorTile),
                "RefreshHighlights debe repintar las puertas por encima del rango.");
        }

        [Test]
        public void RefreshHighlights_NoActiveSelection_DoesNotPaint()
        {
            // Arrange
            var tile = RegisterTileRenderer(1, 0);

            // Act
            _controller.RefreshHighlights();

            // Assert — sin selección no debe tocar nada.
            AssertColorApprox(default, PaintedColor(tile),
                "Sin selección activa RefreshHighlights es no-op.");
        }

        // Stub mínimo: FindPath devuelve un camino fijo — el A* real se cubre en
        // los tests de MovementService.
        private sealed class StubMovementService : IMovementService
        {
            public List<GridCoord> PathToReturn = new List<GridCoord>();

            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => PathToReturn;

            public bool Move(Guid entity, GridCoord destination) => true;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved
            {
                add { }
                remove { }
            }
        }
    }
}
