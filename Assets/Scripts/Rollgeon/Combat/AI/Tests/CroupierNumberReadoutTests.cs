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

        [Test]
        public void WheelNumber_WithNothingInTheAir_WritesNothing()
        {
            // Desde que el sector detona hasta el canto siguiente no hay
            // número, y un "0" o un label vacío colgado del hub se lee como un bug del prop.
            Assert.AreEqual(string.Empty,
                CroupierWheelNumberView.Format(null, CroupierWheelNumberView.DefaultSeparator));
            Assert.AreEqual(string.Empty,
                CroupierWheelNumberView.Format(new List<int>(), CroupierWheelNumberView.DefaultSeparator));
        }

        [Test]
        public void WheelNumber_PhaseOne_WritesTheSingleNumber_Bare()
        {
            var sung = new List<int> { 3 };

            string text = CroupierWheelNumberView.Format(sung, CroupierWheelNumberView.DefaultSeparator);

            Assert.AreEqual("3", text);
        }

        [Test]
        public void WheelNumber_PhaseTwo_WritesBothNumbers()
        {
            var sung = new List<int> { 3, 5 };

            string text = CroupierWheelNumberView.Format(sung, CroupierWheelNumberView.DefaultSeparator);

            Assert.AreEqual("3 / 5", text);
        }

        [Test]
        public void SectorQuads_UseTheCroupierBrass_NotTheGenericWarningOrange()
        {
            // Con el naranja de fábrica el bloque se ve igual que el telegraph de
            // cualquier otro jefe y nada lo ata a la rueda.
            CroupierSectorTelegraph.Mark(_bossGuid, slot: 0, sector: 2, damage: 12, kind: AttackKind.BasicAttack);

            var overlay = (ThreatTelegraphOverlay)ThreatTelegraphOverlay.ResolveOrCreate();
            var quads = overlay.ActiveQuadsOf(CroupierSectorTelegraph.SlotGuid(_bossGuid, 0));

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
