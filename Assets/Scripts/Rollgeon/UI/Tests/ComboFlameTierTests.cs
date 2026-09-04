using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="ComboFlameTier"/>: el tier de la llama de combo armado se decide por
    /// posición en el catálogo ordenado por Priority (mitad baja = 1, mitad alta = 2), no por el
    /// valor de Priority, y degrada a tier 1 cuando el catálogo no puede responder.
    /// </summary>
    [TestFixture]
    public class ComboFlameTierTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void Teardown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void Resolve_EmptyComboId_ReturnsOff()
        {
            // Arrange
            var catalog = Catalog(Combo("combo.pair", 8));

            // Act / Assert
            Assert.AreEqual(ComboFlameTier.Off, ComboFlameTier.Resolve(catalog, null));
            Assert.AreEqual(ComboFlameTier.Off, ComboFlameTier.Resolve(catalog, string.Empty));
        }

        [Test]
        public void Resolve_NullCatalog_ReturnsLow()
        {
            // Act
            int tier = ComboFlameTier.Resolve(null, "combo.generala");

            // Assert — que haya fuego pesa más que acertar el tier.
            Assert.AreEqual(ComboFlameTier.Low, tier);
        }

        [Test]
        public void Resolve_UnknownComboId_ReturnsLow()
        {
            // Arrange
            var catalog = Catalog(Combo("combo.pair", 8), Combo("combo.poker", 55));

            // Act
            int tier = ComboFlameTier.Resolve(catalog, "combo.not_in_catalog");

            // Assert
            Assert.AreEqual(ComboFlameTier.Low, tier);
        }

        [Test]
        public void Resolve_RealCatalogOfEight_SplitsAtFullHouse()
        {
            // Arrange — prioridades reales del proyecto; Generala fuerza int.MaxValue por override.
            var catalog = RealCatalog();

            // Act / Assert
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "combo.higher_number"));
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "combo.pair"));
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "combo.double_pair"));
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "combo.trio"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.full_house"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.ladder"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.poker"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.generala"));
        }

        [Test]
        public void Resolve_UnsortedEntriesWithNull_SameSplitAsSorted()
        {
            // Arrange — el catálogo real viene en orden de autoría y puede traer huecos.
            var catalog = Catalog(
                Combo("combo.poker", 55),
                null,
                Combo("combo.pair", 8),
                Combo("combo.full_house", 35),
                Combo("combo.trio", 22),
                Combo("combo.ladder", 40),
                Combo("combo.higher_number", 5),
                Combo("combo.double_pair", 15),
                Combo<Combo_Generala>("combo.generala", 90));

            // Act / Assert
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "combo.trio"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.full_house"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "combo.generala"));
        }

        [Test]
        public void Resolve_OddCount_MiddleEntryIsLow()
        {
            // Arrange — 9 combos: índices 0..4 tier 1, 5..8 tier 2 ("hasta la mitad" inclusive).
            var catalog = Catalog(
                Combo("c0", 1), Combo("c1", 2), Combo("c2", 3), Combo("c3", 4), Combo("c4", 5),
                Combo("c5", 6), Combo("c6", 7), Combo("c7", 8), Combo("c8", 9));

            // Act / Assert
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "c4"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "c5"));
        }

        [Test]
        public void Resolve_TiedPriorities_CatalogOrderBreaksTie()
        {
            // Arrange — dos combos con la misma Priority: el que aparece antes en el catálogo
            // queda antes (sort estable), así el corte no depende del azar.
            var catalog = Catalog(Combo("a", 10), Combo("b", 10), Combo("c", 10), Combo("d", 10));

            // Act / Assert
            Assert.AreEqual(ComboFlameTier.Low, ComboFlameTier.Resolve(catalog, "b"));
            Assert.AreEqual(ComboFlameTier.High, ComboFlameTier.Resolve(catalog, "c"));
        }

        // ---- Helpers -----------------------------------------------------------

        private ComboCatalogSO RealCatalog() => Catalog(
            Combo("combo.double_pair", 15),
            Combo("combo.ladder", 40),
            Combo("combo.full_house", 35),
            Combo<Combo_Generala>("combo.generala", 90),
            Combo("combo.pair", 8),
            Combo("combo.poker", 55),
            Combo("combo.higher_number", 5),
            Combo("combo.trio", 22));

        private BaseComboSO Combo(string id, int priority) => Combo<Combo_Par>(id, priority);

        private BaseComboSO Combo<T>(string id, int priority) where T : BaseComboSO
        {
            var combo = ComboTestUtils.CreateCombo<T>(id, baseDamage: 1, priority: priority);
            _created.Add(combo);
            return combo;
        }

        private ComboCatalogSO Catalog(params BaseComboSO[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<ComboCatalogSO>();
            ComboTestUtils.SetField(catalog, "_entries", new List<BaseComboSO>(entries));
            _created.Add(catalog);
            return catalog;
        }
    }
}
