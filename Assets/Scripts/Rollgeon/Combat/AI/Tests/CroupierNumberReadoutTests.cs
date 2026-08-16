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
    /// Tests de cómo se lee el número del Croupier: el que se escribe en el centro de la ruleta
    /// (<see cref="CroupierWheelNumberView"/>) y el latón con que el paño marca el sector que anuncia.
    /// </summary>
    /// <remarks>
    /// El número es el pivote del jefe entero — el sector que detona y el dado que confisca — y hasta
    /// ahora no se dibujaba en ningún lado: sus únicos consumidores eran nodos de IA. Estos tests
    /// fijan que la ruleta lo diga y que el bloque del paño quede atado a ella por el matiz.
    /// </remarks>
    [TestFixture]
    public class CroupierNumberReadoutTests
    {
        private GridManager _grid;
        private ThreatenedAreaService _threat;
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
        }

        [TearDown]
        public void TearDown()
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();

            DestroyLeftover("ThreatTelegraphOverlay");

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
        // El bloque del paño que anuncia
        // =====================================================================

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

        private static void AssertRgb(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, "R");
            Assert.AreEqual(expected.g, actual.g, 0.001f, "G");
            Assert.AreEqual(expected.b, actual.b, 0.001f, "B");
        }
    }
}
