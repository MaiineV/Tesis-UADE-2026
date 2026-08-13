using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cubre el flujo AoE del <see cref="SelectionController"/>: expansión del ancla en
    /// Complete() (único punto del flujo manual), preview del área con el estilo "aoe"
    /// en hover, y el count dinámico usando el OwnerGuid del request.
    /// </summary>
    [TestFixture]
    public sealed class SelectionControllerAoeTests
    {
        private static readonly int ColorId = Shader.PropertyToID("_HitFlashColor");

        private static readonly Color AoeColor = new Color(1f, 0.6f, 0.1f, 0.55f);
        private static readonly Color AttackColor = new Color(1f, 0.3f, 0.3f, 0.6f);

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

            // Pasillo 5x1: owner en (0,0), enemigos en (2,0) y (3,0).
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0));
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

        private static SelectionSettings AoeAttack(int radius)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = 3,
                TargetMode = TargetMode.Aoe,
                AoeShape = AoeShape.Radius,
                AoeRadius = radius,
                AutoAccept = true,
            };
        }

        private void BeginSelection(SelectionSettings settings, params GridCoord[] validTargets)
        {
            _controller.BeginSelection(new SelectionRequest
            {
                OwnerGuid = _owner,
                Settings = settings,
                HighlightStyle = "attack",
                ValidTargets = validTargets.Select(TargetRef.At).ToList(),
            });
        }

        [Test]
        public void OnTargetClicked_AoeAnchorWithAutoAccept_CompletesWithExpandedTargets()
        {
            // Arrange — ancla (2,0), radio 1: el enemigo de (3,0) entra al área.
            BeginSelection(AoeAttack(radius: 1), new GridCoord(2, 0));
            TargetSelectionResult result = null;
            _controller.OnSelectionCompleted += r => result = r;

            // Act — en AoE los picks requeridos son 1: el primer click completa.
            _controller.OnTargetClicked(TargetRef.At(new GridCoord(2, 0)));

            // Assert
            Assert.IsNotNull(result, "AutoAccept debe completar al primer click válido.");
            Assert.IsTrue(result.WasCompleted);
            Assert.AreEqual(2, result.SelectedTargets.Count,
                "El resultado debe llegar expandido (ancla + área filtrada).");
            Assert.IsTrue(result.SelectedTargets.Any(t => t.Coord == new GridCoord(2, 0)));
            Assert.IsTrue(result.SelectedTargets.Any(t => t.Coord == new GridCoord(3, 0)));
        }

        [Test]
        public void Complete_SingleMode_ResultUnexpanded()
        {
            // Arrange — regresión: en Single el resultado es exactamente lo clickeado.
            var settings = AoeAttack(radius: 1);
            settings.TargetMode = TargetMode.Single;
            BeginSelection(settings, new GridCoord(2, 0), new GridCoord(3, 0));
            TargetSelectionResult result = null;
            _controller.OnSelectionCompleted += r => result = r;

            // Act
            _controller.OnTargetClicked(TargetRef.At(new GridCoord(2, 0)));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.SelectedTargets.Count,
                "Single no expande: un click, un target.");
            Assert.AreEqual(new GridCoord(2, 0), result.SelectedTargets[0].Coord);
        }

        [Test]
        public void OnTargetHovered_AoeValidAnchor_PaintsAoeAreaStyle()
        {
            // Arrange
            RegisterTileRenderer(2, 0);
            var splash = RegisterTileRenderer(3, 0);
            BeginSelection(AoeAttack(radius: 1), new GridCoord(2, 0));

            // Act
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(2, 0)));

            // Assert — la celda del splash (fuera de los targets clickeables) se pinta
            // con el estilo "aoe" para previewar el área afectada.
            AssertColorApprox(AoeColor, PaintedColor(splash),
                "Hovear un ancla válida debe pintar el área AoE.");
        }

        [Test]
        public void OnTargetHovered_AoeMovedOffAnchor_ClearsAoeOverlay()
        {
            // Arrange — la regresión más fácil: el tinte AoE puede caer FUERA del rango
            // pintado; si la limpieza solo repinta _validCoords, queda pegado.
            var anchor = RegisterTileRenderer(2, 0);
            var splash = RegisterTileRenderer(3, 0);
            BeginSelection(AoeAttack(radius: 1), new GridCoord(2, 0));
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(2, 0)));

            // Act — el cursor sale del ancla hacia una celda inválida.
            _controller.OnTargetHovered(TargetRef.At(new GridCoord(4, 0)));

            // Assert — el splash vuelve a quedar sin tinte y el ancla recupera "attack".
            AssertColorApprox(default, PaintedColor(splash),
                "El tinte AoE fuera del rango debe limpiarse al salir del ancla.");
            AssertColorApprox(AttackColor, PaintedColor(anchor),
                "El target válido debe volver a su estilo de selección.");
        }

        [Test]
        public void OnTargetClicked_DynamicCount_UsesRequestOwnerGuid()
        {
            // Arrange — regresión: OnTargetClicked pasaba ReadInfo default y el reader
            // veía siempre un guid vacío. El stub devuelve 2 solo con guid válido.
            var reader = new GuidSensitiveCountReader();
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = 3,
                IsConstantSelectionCount = false,
                SelectionCountReader = reader,
                AutoAccept = true,
            };
            BeginSelection(settings, new GridCoord(2, 0), new GridCoord(3, 0));
            TargetSelectionResult result = null;
            _controller.OnSelectionCompleted += r => result = r;

            // Act — con required=2 el primer click NO completa; el segundo sí.
            _controller.OnTargetClicked(TargetRef.At(new GridCoord(2, 0)));
            Assert.IsNull(result, "Con count dinámico 2 el primer click no debe completar.");
            _controller.OnTargetClicked(TargetRef.At(new GridCoord(3, 0)));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.SelectedTargets.Count);
            Assert.AreEqual(_owner, reader.LastOwnerGuid,
                "El controller debe pasar el OwnerGuid del request al reader.");
        }

        // Devuelve 2 solo si recibió un guid real: si el call site pasa ReadInfo default,
        // required cae a 1 y el test falla en el primer click.
        private sealed class GuidSensitiveCountReader : ISelectionCountReader
        {
            public Guid LastOwnerGuid;

            public int Read(ReadInfo info)
            {
                LastOwnerGuid = info.ownerGuid;
                return info.ownerGuid == Guid.Empty ? 1 : 2;
            }
        }
    }
}
