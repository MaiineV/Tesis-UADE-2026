using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Tests
{
    [TestFixture]
    public sealed class ThreatTelegraphOverlayTests
    {
        private ThreatTelegraphOverlay _overlay;
        private GridManager _grid;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5), Vector3.zero, 1f);
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _overlay = new ThreatTelegraphOverlay();
            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _overlay?.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static List<GridCoord> Tiles(params (int x, int y)[] coords)
        {
            var list = new List<GridCoord>();
            foreach (var (x, y) in coords) list.Add(new GridCoord(x, y));
            return list;
        }

        private static List<GridCoord> WholeRoom(int width, int height)
        {
            var list = new List<GridCoord>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    list.Add(new GridCoord(x, y));
            return list;
        }

        [Test]
        public void Show_CreatesActiveQuadsOverThreatenedTiles()
        {
            // Act
            _overlay.Show(_boss, Tiles((1, 0), (2, 0)));

            // Assert
            Assert.AreEqual(2, _overlay.ActiveQuadCount);

            var root = GameObject.Find("ThreatTelegraphOverlay");
            Assert.IsNotNull(root, "El overlay debe crear su root pooled.");

            var expected = _grid.GridToWorld(new GridCoord(1, 0)) + Vector3.up * _overlay.YOffset;
            bool found = false;
            foreach (Transform child in root.transform)
                if (child.gameObject.activeSelf && (child.position - expected).sqrMagnitude < 1e-6f)
                    found = true;
            Assert.IsTrue(found, "Debe haber un quad centrado sobre la casilla amenazada (1,0).");
        }

        [Test]
        public void Show_SameSource_ReplacesPreviousArea()
        {
            // Arrange
            _overlay.Show(_boss, Tiles((0, 0), (1, 0), (2, 0)));

            // Act — el boss re-telegrafía un área distinta.
            _overlay.Show(_boss, Tiles((4, 4)));

            // Assert
            Assert.AreEqual(1, _overlay.ActiveQuadCount,
                "Re-marcar debe reemplazar el área previa, no acumularla.");
        }

        [Test]
        public void Clear_Source_DeactivatesOnlyItsQuads()
        {
            // Arrange — dos bosses con áreas propias.
            var otherBoss = Guid.NewGuid();
            _overlay.Show(_boss, Tiles((0, 0), (1, 0)));
            _overlay.Show(otherBoss, Tiles((3, 3)));

            // Act
            _overlay.Clear(_boss);

            // Assert
            Assert.AreEqual(1, _overlay.ActiveQuadCount,
                "Clear de un boss no debe apagar el telegraph del otro.");
        }

        [Test]
        public void Show_SurvivesTileHighlightClearAll()
        {
            // Arrange — el contrato de coexistencia (BUG del AOE + move): el tinte
            // de piso puede pintarse y limpiarse entero sin tocar el overlay.
            var highlight = new TileHighlightService();
            _overlay.Show(_boss, Tiles((1, 1), (2, 1)));

            // Act
            highlight.Highlight(Tiles((1, 1), (2, 1)), "move");
            highlight.ClearAll();

            // Assert
            Assert.AreEqual(2, _overlay.ActiveQuadCount,
                "El overlay del telegraph debe sobrevivir al ClearAll del highlight de movimiento.");
        }

        [Test]
        public void ResolveOrCreate_OnCombatEnd_ClearsAllOverlays()
        {
            // Arrange — la instancia registrada vía ResolveOrCreate escucha el fin
            // de combate para no dejar quads colgados.
            var service = ThreatTelegraphOverlay.ResolveOrCreate();
            service.Show(_boss, Tiles((1, 0)));
            Assume.That(((ThreatTelegraphOverlay)service).ActiveQuadCount, Is.EqualTo(1));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(0, ((ThreatTelegraphOverlay)service).ActiveQuadCount);

            ((ThreatTelegraphOverlay)service).Dispose();
        }

        // =====================================================================
        // Costo por frame: el telegraph de sala entera
        // =====================================================================

        [Test]
        public void Show_WholeRoom_SharesOneMaterialAndCarriesNoPropertyBlock()
        {
            // Arrange — el caso que se midió: un ataque que telegrafía la sala entera.
            var wholeRoom = WholeRoom(5, 5);

            // Act
            _overlay.Show(_boss, wholeRoom);

            // Assert
            var quads = _overlay.ActiveQuadsOf(_boss);
            Assume.That(quads.Count, Is.EqualTo(wholeRoom.Count));

            var shared = quads[0].Renderer.sharedMaterial;
            Assert.IsNotNull(shared, "Sin material los quads no se dibujan.");

            foreach (var quad in quads)
            {
                Assert.AreSame(shared, quad.Renderer.sharedMaterial,
                    "Un material por quad multiplica los SetPass calls del telegraph por la cantidad " +
                    "de casillas amenazadas.");
                Assert.IsFalse(quad.Renderer.HasPropertyBlock(),
                    "Un MaterialPropertyBlock por renderer lo saca del SRP Batcher sin importar el " +
                    "shader, que es exactamente el costo que se vino a sacar.");
            }
        }

        [Test]
        public void Pulse_OnAStyleWithoutHeartbeat_StopsWritingAfterTheShow()
        {
            // Arrange — Detonating tiene PulseSpeed 0: su alpha es constante, así que repintarlo por
            // frame era trabajo puro.
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Detonating);
            var group = _overlay.ActiveQuadsOf(_boss)[0].Group;
            Assume.That(group, Is.Not.Null);

            // Act / Assert
            Assert.IsFalse(group.Pulse(Time.time),
                "El Show ya dejó el alpha escrito: repetirlo el mismo frame es una escritura de más.");
            Assert.IsFalse(group.Pulse(Time.time + 10f),
                "Un estado sin latido no puede seguir escribiendo su material cada frame.");
        }

        [Test]
        public void Pulse_OnAPulsingStyle_KeepsReachingTheMaterialWhenTheAlphaMoves()
        {
            // Arrange
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked);
            var group = _overlay.ActiveQuadsOf(_boss)[0].Group;
            var marked = _overlay.StyleOf(ThreatOverlayState.Marked);

            // Un cuarto de período por paso: el seno no puede quedarse quieto en cuatro pasos, así
            // que el test no depende de en qué punto del latido arrancó Time.time.
            float quarter = Mathf.PI * 0.5f / marked.PulseSpeed;

            // Act
            bool wrote = false;
            for (int step = 1; step <= 4 && !wrote; step++)
                wrote = group.Pulse(Time.time + quarter * step);

            // Assert
            Assert.IsTrue(wrote,
                "El skip es por alpha igual, no por estado: si el latido deja de llegar al material, " +
                "el telegraph queda congelado.");
        }

        [Test]
        public void Dispose_DestroysEveryCachedMaterial_NotJustOne()
        {
            // Arrange — dos pares (estado, matiz) = dos materiales en el cache.
            var hazard = Guid.NewGuid();
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked, Color.red);
            _overlay.Show(hazard, Tiles((3, 3)), ThreatOverlayState.Safe, Color.cyan);

            var first = _overlay.ActiveQuadsOf(_boss)[0].Renderer.sharedMaterial;
            var second = _overlay.ActiveQuadsOf(hazard)[0].Renderer.sharedMaterial;
            Assume.That(first, Is.Not.SameAs(second));

            // Act
            _overlay.Dispose();

            // Assert — el fake-null de Unity es la única señal de que el nativo se fue, así que hay
            // que pasar por el == de UnityEngine.Object y no por Is.Null de NUnit.
            Assert.IsTrue(first == null, "Dispose dejó colgado el material del primer par.");
            Assert.IsTrue(second == null,
                "Dispose bajó un solo material: el resto leakea un material por par y por run.");
        }
    }
}
