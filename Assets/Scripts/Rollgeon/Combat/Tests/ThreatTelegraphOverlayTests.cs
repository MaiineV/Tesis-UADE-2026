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
    }
}
