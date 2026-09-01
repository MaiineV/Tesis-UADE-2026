using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Items.Active;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Mecanismos propios de <see cref="ActiveItemFamily.Precision"/> y
    /// <see cref="ActiveItemFamily.Control"/> (GDD "Ítems Activos" §24, TBD-22 resuelto).
    /// Son las dos únicas familias que NO reparten por tercios.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemFamilyBandsTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // Precisión — distancia al valor objetivo
        // ------------------------------------------------------------------

        [TestCase(4, ActiveItemBand.Positive)]  // acierto exacto
        [TestCase(3, ActiveItemBand.Mixed)]     // a 1
        [TestCase(5, ActiveItemBand.Mixed)]     // a 1 del otro lado
        [TestCase(2, ActiveItemBand.Negative)]  // a 2
        [TestCase(6, ActiveItemBand.Negative)]
        [TestCase(1, ActiveItemBand.Negative)]
        public void test_precision_bandFollowsDistanceToTheTarget(int roll, ActiveItemBand expected)
        {
            // Act — objetivo 4 sobre D6.
            var band = ActiveItemBands.ResolvePrecision(roll, faces: 6, target: 4);

            // Assert
            Assert.AreEqual(expected, band);
        }

        [Test]
        public void test_precision_targetAtTheEdge_leavesMixedWithOnlyOneFace()
        {
            // Arrange — objetivo en la cara 1 de un D6. El GDD lo nombra como
            // consecuencia natural de la formula, no como caso especial a manejar aparte.
            var item = NewItem(DiceType.D6, ActiveItemFamily.Precision);
            item.PrecisionTarget = 1;

            // Act
            var mixed = ActiveItemBands.FacesOf(ActiveItemBand.Mixed, item);
            var positive = ActiveItemBands.FacesOf(ActiveItemBand.Positive, item);

            // Assert
            CollectionAssert.AreEqual(new[] { 1 }, positive);
            CollectionAssert.AreEqual(new[] { 2 }, mixed, "solo una cara de margen, no dos");
        }

        [Test]
        public void test_precision_alwaysHasAConsolationBand()
        {
            // Arrange — la regla de no-binariedad: acertar de casualidad tiene banda
            // intermedia, nunca es todo o nada.
            foreach (var die in AllDice())
            {
                int faces = die.MaxFace();
                for (int target = 1; target <= faces; target++)
                {
                    var item = NewItem(die, ActiveItemFamily.Precision);
                    item.PrecisionTarget = target;

                    // Act + Assert
                    CollectionAssert.IsNotEmpty(ActiveItemBands.FacesOf(ActiveItemBand.Mixed, item),
                        $"d{faces} objetivo {target}: la banda mixta quedo vacia");
                }
            }
        }

        // ------------------------------------------------------------------
        // Control — paridad + mitad superior
        // ------------------------------------------------------------------

        [TestCase(6, ActiveItemBand.Positive)]  // par y mitad superior
        [TestCase(4, ActiveItemBand.Positive)]
        [TestCase(2, ActiveItemBand.Mixed)]     // par pero mitad inferior
        [TestCase(5, ActiveItemBand.Mixed)]     // mitad superior pero impar
        [TestCase(1, ActiveItemBand.Negative)]  // ni una ni otra
        [TestCase(3, ActiveItemBand.Negative)]
        public void test_control_evenTarget_crossesParityAndUpperHalf(int roll, ActiveItemBand expected)
        {
            // Act — paridad par sobre D6: mitad superior es 4, 5, 6.
            var band = ActiveItemBands.ResolveControl(roll, faces: 6, ActiveItemParity.Even);

            // Assert
            Assert.AreEqual(expected, band);
        }

        [TestCase(5, ActiveItemBand.Positive)]  // impar y mitad superior
        [TestCase(1, ActiveItemBand.Mixed)]     // impar pero mitad inferior
        [TestCase(6, ActiveItemBand.Mixed)]     // mitad superior pero par
        [TestCase(2, ActiveItemBand.Negative)]
        public void test_control_oddTarget_crossesParityAndUpperHalf(int roll, ActiveItemBand expected)
        {
            // Act
            var band = ActiveItemBands.ResolveControl(roll, faces: 6, ActiveItemParity.Odd);

            // Assert
            Assert.AreEqual(expected, band);
        }

        [Test]
        public void test_control_bandsAreNotContiguous()
        {
            // Arrange — esto es lo que rompe el tooltip por rangos: con paridad par sobre
            // D6 la banda mixta son las caras 2 y 5, que no son un tramo.
            var item = NewItem(DiceType.D6, ActiveItemFamily.Control);
            item.ControlParity = ActiveItemParity.Even;

            // Act
            var mixed = ActiveItemBands.FacesOf(ActiveItemBand.Mixed, item);

            // Assert
            CollectionAssert.AreEqual(new[] { 2, 5 }, mixed);
            Assert.AreEqual("2, 5", ActiveItemBands.DescribeFaces(ActiveItemBand.Mixed, item),
                "el tooltip tiene que listar las caras sueltas, no fingir un rango");
        }

        [Test]
        public void test_control_noBandIsEmptyOnAnyDie()
        {
            // Arrange — una banda vacia seria un efecto autorado que nunca corre.
            foreach (var die in AllDice())
            {
                foreach (var parity in new[] { ActiveItemParity.Even, ActiveItemParity.Odd })
                {
                    var item = NewItem(die, ActiveItemFamily.Control);
                    item.ControlParity = parity;

                    // Act + Assert
                    foreach (ActiveItemBand band in System.Enum.GetValues(typeof(ActiveItemBand)))
                    {
                        CollectionAssert.IsNotEmpty(ActiveItemBands.FacesOf(band, item),
                            $"d{die.MaxFace()} {parity}: la banda {band} quedo vacia");
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Las demás familias siguen por tercios
        // ------------------------------------------------------------------

        [TestCase(ActiveItemFamily.Potencia)]
        [TestCase(ActiveItemFamily.Estabilidad)]
        [TestCase(ActiveItemFamily.Riesgo)]
        [TestCase(ActiveItemFamily.Sacrificio)]
        public void test_otherFamilies_stillSplitByThirds(ActiveItemFamily family)
        {
            // Arrange
            var item = NewItem(DiceType.D6, family);

            // Act + Assert — el reparto confirmado del GDD para D6.
            Assert.AreEqual("1-2", ActiveItemBands.DescribeFaces(ActiveItemBand.Negative, item));
            Assert.AreEqual("3-4", ActiveItemBands.DescribeFaces(ActiveItemBand.Mixed, item));
            Assert.AreEqual("5-6", ActiveItemBands.DescribeFaces(ActiveItemBand.Positive, item));
        }

        [Test]
        public void test_describeFaces_collapsesContiguousRunsAndListsGaps()
        {
            // Arrange — Precision con objetivo 4 sobre D6: negativa son 1, 2 y 6.
            var item = NewItem(DiceType.D6, ActiveItemFamily.Precision);
            item.PrecisionTarget = 4;

            // Act + Assert
            Assert.AreEqual("1-2, 6", ActiveItemBands.DescribeFaces(ActiveItemBand.Negative, item));
            Assert.AreEqual("3, 5", ActiveItemBands.DescribeFaces(ActiveItemBand.Mixed, item));
            Assert.AreEqual("4", ActiveItemBands.DescribeFaces(ActiveItemBand.Positive, item));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static IEnumerable<DiceType> AllDice()
            => new[] { DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12, DiceType.D20 };

        private ItemSO NewItem(DiceType die, ActiveItemFamily family)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.test";
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.ActiveFamily = family;
            _spawned.Add(item);
            return item;
        }
    }
}
