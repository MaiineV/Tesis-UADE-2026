using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cubre el preview de trayectoria por dirección (Diseño A4, Feature#0084 — Justa de
    /// Justicia / Grapple Claw): un request con <see cref="SelectionRequest.HoverPreview"/>
    /// pinta la trayectoria calculada por el efecto activo al hovear un target válido, con
    /// el mismo patrón "ancho" que el preview AoE (ClearAll + repintado completo, ver
    /// <see cref="SelectionControllerAoeTests"/>).
    /// </summary>
    [TestFixture]
    public sealed class SelectionControllerHoverPreviewTests
    {
        private static readonly int ColorId = Shader.PropertyToID("_HitFlashColor");

        private static readonly Color AttackColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        private static readonly Color AoeColor = new Color(1f, 0.6f, 0.1f, 0.55f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private TileHighlightService _highlight;
        private SelectionController _controller;
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _owner = Guid.NewGuid();

            // Pasillo 6x1: owner en (0,0), el resto de las celdas quedan libres para
            // registrar renderers de trayectoria.
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

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

        private void BeginSelection(SelectionRequest request) => _controller.BeginSelection(request);

        [Test]
        public void OnTargetHovered_ValidTargetWithHoverPreview_PaintsTrajectoryWithStyle()
        {
            // Arrange — target "dirección" en (2,0); la trayectoria calculada por el efecto
            // (simula PreviewTrajectory de A4) llega hasta (4,0). Estilo custom "aoe" para
            // probar que respeta HoverPreviewStyle y no cae al default "path".
            var target = RegisterTileRenderer(2, 0);
            var midTile = RegisterTileRenderer(3, 0);
            var farTile = RegisterTileRenderer(4, 0);
            BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = new SelectionSettings { TargetMode = TargetMode.Single },
                HighlightStyle = "attack",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 0)) },
                HoverPreview = coord => new List<GridCoord> { coord, new GridCoord(3, 0), new GridCoord(4, 0) },
                HoverPreviewStyle = "aoe",
            });

            // Act
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(2, 0)));

            // Assert — toda la trayectoria (incluida la casilla del target) queda con el
            // estilo custom pedido por el request, no con el "path" default.
            AssertColorApprox(AoeColor, PaintedColor(target), "El target debe quedar con el estilo de la trayectoria.");
            AssertColorApprox(AoeColor, PaintedColor(midTile), "La casilla intermedia de la trayectoria debe pintarse.");
            AssertColorApprox(AoeColor, PaintedColor(farTile), "El extremo de la trayectoria debe pintarse.");
        }

        [Test]
        public void OnTargetHovered_MovedOffValidTarget_ClearsTrajectoryPreview()
        {
            // Arrange — regresión equivalente a la del AoE: la trayectoria puede exceder
            // _validCoords, así que salir del target tiene que limpiarla entera con ClearAll.
            var target = RegisterTileRenderer(2, 0);
            var farTile = RegisterTileRenderer(4, 0);
            var elsewhere = RegisterTileRenderer(5, 0);
            BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = new SelectionSettings { TargetMode = TargetMode.Single },
                HighlightStyle = "attack",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 0)) },
                HoverPreview = coord => new List<GridCoord> { coord, new GridCoord(4, 0) },
            });
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(2, 0)));

            // Act — el cursor sale hacia una celda que no es target válido.
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(5, 0)));

            // Assert — la trayectoria se limpia y el target vuelve a su estilo de selección.
            AssertColorApprox(default, PaintedColor(farTile), "El extremo de la trayectoria debe limpiarse fuera del target.");
            AssertColorApprox(AttackColor, PaintedColor(target), "El target debe volver a su estilo normal.");
            AssertColorApprox(default, PaintedColor(elsewhere), "La celda hovered fuera de ValidTargets no recibe tinte.");
        }

        [Test]
        public void OnTargetHovered_RequestWithoutHoverPreview_BehavesAsBefore()
        {
            // Arrange — regresión: un request sin HoverPreview (la mayoría) no debe activar
            // la rama nueva; el target simplemente conserva su color de selección normal.
            var target = RegisterTileRenderer(2, 0);
            BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = new SelectionSettings { TargetMode = TargetMode.Single },
                HighlightStyle = "attack",
                ValidTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 0)) },
            });

            // Act
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(2, 0)));

            // Assert
            AssertColorApprox(AttackColor, PaintedColor(target), "Sin HoverPreview el target mantiene su estilo normal, sin trayectoria.");
        }
    }
}
