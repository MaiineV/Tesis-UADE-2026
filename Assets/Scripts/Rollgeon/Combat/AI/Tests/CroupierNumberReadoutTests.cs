using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de las dos mitades que hacen legible el número del Croupier: el que se escribe en el
    /// centro de la ruleta (<see cref="CroupierWheelNumberView"/>) y el que se escribe sobre el bloque
    /// del paño que va a caer (<see cref="CroupierSectorNumberOverlay"/>).
    /// </summary>
    /// <remarks>
    /// El número es el pivote del jefe entero — el sector que detona y el dado que confisca — y hasta
    /// ahora no se dibujaba en ningún lado: sus únicos consumidores eran nodos de IA. Estos tests
    /// fijan que las dos vistas digan el mismo número y que ninguna quede encendida después de detonar.
    /// </remarks>
    [TestFixture]
    public class CroupierNumberReadoutTests
    {
        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private CroupierSectorNumberOverlay _numbers;
        private Guid _bossGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 7));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _bossGuid = Guid.NewGuid();
            _numbers = CroupierSectorNumberOverlay.ResolveOrCreate();
        }

        [TearDown]
        public void TearDown()
        {
            _numbers.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();

            DestroyLeftover("ThreatTelegraphOverlay");
            DestroyLeftover("CroupierSectorNumberOverlay");

            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static void DestroyLeftover(string rootName)
        {
            var leftover = GameObject.Find(rootName);
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);
        }

        // =====================================================================
        // El número en el centro de la ruleta
        // =====================================================================

        [Test]
        public void WheelNumber_WithNothingInTheAir_WritesNothing()
        {
            // Arrange / Act / Assert — desde que el sector detona hasta el canto siguiente no hay
            // número, y un "0" o un label vacío colgado del hub se lee como un bug del prop.
            Assert.AreEqual(string.Empty,
                CroupierWheelNumberView.Format(null, CroupierWheelNumberView.DefaultSeparator));
            Assert.AreEqual(string.Empty,
                CroupierWheelNumberView.Format(new List<int>(), CroupierWheelNumberView.DefaultSeparator));
        }

        [Test]
        public void WheelNumber_PhaseOne_WritesTheSingleNumber_Bare()
        {
            // Arrange
            var sung = new List<int> { 3 };

            // Act
            string text = CroupierWheelNumberView.Format(sung, CroupierWheelNumberView.DefaultSeparator);

            // Assert — sin separador ni adornos: el hub de la ruleta muestra un número y nada más.
            Assert.AreEqual("3", text);
        }

        [Test]
        public void WheelNumber_PhaseTwo_WritesBothNumbers()
        {
            // Arrange — "pleno y color": la fase 2 canta dos, y los dos van a caer.
            var sung = new List<int> { 3, 5 };

            // Act
            string text = CroupierWheelNumberView.Format(sung, CroupierWheelNumberView.DefaultSeparator);

            // Assert
            Assert.AreEqual("3 / 5", text);
        }

        // =====================================================================
        // El número sobre el bloque del paño
        // =====================================================================

        [Test]
        public void SectorNumber_MarksTheSungNumber_OnTheSector()
        {
            // Arrange
            var tiles = ThreatAreaShape.ComputeRoomSector(_grid, 2);
            Assume.That(tiles, Is.Not.Empty);

            // Act
            bool marked = CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 2, damage: 12,
                kind: AttackKind.BasicAttack);

            // Assert
            Assert.IsTrue(marked);
            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            Assert.AreEqual(2, _numbers.NumberOf(slotGuid),
                "El bloque tiene que decir el mismo número que canta la rueda.");
        }

        [Test]
        public void SectorNumber_StandsInsideTheSectorItAnnounces()
        {
            // Arrange — el número flotando fuera del bloque señalaría la casilla equivocada.
            var tiles = ThreatAreaShape.ComputeRoomSector(_grid, 5);

            // Act
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 5, damage: 12, kind: AttackKind.BasicAttack);

            // Assert
            var slotGuid = CroupierSectorTelegraph.SlotGuid(_bossGuid, 0);
            var coord = _numbers.CoordOf(slotGuid);
            Assert.IsTrue(coord.HasValue);
            CollectionAssert.Contains(tiles, coord.Value);
        }

        [Test]
        public void SectorNumber_PhaseTwo_KeepsOneNumberPerSlot()
        {
            // Arrange / Act — dos números en el aire, cada uno sobre su bloque.
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 1, damage: 12, kind: AttackKind.BasicAttack);
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 1, sector: 6, damage: 12, kind: AttackKind.BasicAttack);

            // Assert
            Assert.AreEqual(1, _numbers.NumberOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
            Assert.AreEqual(6, _numbers.NumberOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 1)));
            Assert.AreEqual(2, _numbers.ActiveLabelCount, "Un label por slot, no uno por casilla.");
        }

        [Test]
        public void SectorNumber_ReMarkingTheSameSlot_MovesTheLabel_InsteadOfAddingAnother()
        {
            // Arrange — es lo que pasa cada vez que el jugador corre la rueda.
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 1, damage: 12, kind: AttackKind.BasicAttack);

            // Act
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 2, damage: 12, kind: AttackKind.BasicAttack);

            // Assert
            Assert.AreEqual(1, _numbers.ActiveLabelCount, "Correr la rueda no deja el número viejo atrás.");
            Assert.AreEqual(2, _numbers.NumberOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
        }

        [Test]
        public void SectorNumber_ClearOverlay_TakesTheNumberDownWithTheQuads()
        {
            // Arrange
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 3, damage: 12, kind: AttackKind.BasicAttack);
            Assume.That(_numbers.ActiveLabelCount, Is.EqualTo(1));

            // Act
            CroupierSectorTelegraph.ClearOverlay(_bossGuid, slot: 0);

            // Assert — si el número sobrevive al quad, el paño queda anunciando un golpe que ya cayó.
            Assert.AreEqual(0, _numbers.ActiveLabelCount);
            Assert.AreEqual(0, _numbers.NumberOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0)));
        }

        [Test]
        public void SectorNumber_CombatEnd_ClearsEverything()
        {
            // Arrange
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 4, damage: 12, kind: AttackKind.BasicAttack);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(0, _numbers.ActiveLabelCount, "El número no se filtra a la pelea siguiente.");
        }

        [Test]
        public void SectorQuads_UseTheCroupierBrass_NotTheGenericWarningOrange()
        {
            // Arrange — con el naranja de fábrica, el bloque del Croupier se veía igual que el
            // telegraph de cualquier otro jefe y nada lo ataba a la rueda.
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 2, damage: 12, kind: AttackKind.BasicAttack);

            // Act
            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            var quads = overlay.ActiveQuadsOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0));

            // Assert
            Assume.That(quads, Is.Not.Empty);
            AssertRgb(CroupierSectorTelegraph.SectorTint, quads[0].Tint);
            Assert.That(quads[0].Tint.g,
                Is.Not.EqualTo(ThreatTelegraphOverlay.DefaultTint.g).Within(0.001f),
                "El sector cantado no puede compartir matiz con el telegraph genérico.");
        }

        // =====================================================================
        // Centro del bloque (puro)
        // =====================================================================

        [Test]
        public void TryCenter_EmptySet_FindsNothing()
        {
            Assert.IsFalse(CroupierSectorNumberOverlay.TryCenter(new List<GridCoord>(), out _));
            Assert.IsFalse(CroupierSectorNumberOverlay.TryCenter(null, out _));
        }

        [Test]
        public void TryCenter_PicksTheMiddleTile_OfARectangle()
        {
            // Arrange — 3×3 con centro exacto en (1,1).
            var tiles = new List<GridCoord>();
            for (int x = 0; x <= 2; x++)
            for (int y = 0; y <= 2; y++)
                tiles.Add(new GridCoord(x, y));

            // Act
            bool found = CroupierSectorNumberOverlay.TryCenter(tiles, out var center);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new GridCoord(1, 1), center);
        }

        [Test]
        public void TryCenter_AlwaysLandsOnATileOfTheSet_EvenWhenTheAverageFallsOutside()
        {
            // Arrange — bloque en "L": el promedio crudo cae en el hueco, y un número flotando ahí
            // señalaría una casilla que no pertenece al sector.
            var tiles = new List<GridCoord>
            {
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
                new GridCoord(0, 1),
                new GridCoord(0, 2),
            };

            // Act
            bool found = CroupierSectorNumberOverlay.TryCenter(tiles, out var center);

            // Assert
            Assert.IsTrue(found);
            CollectionAssert.Contains(tiles, center);
        }

        private static void AssertRgb(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, "R");
            Assert.AreEqual(expected.g, actual.g, 0.001f, "G");
            Assert.AreEqual(expected.b, actual.b, 0.001f, "B");
        }
    }
}
