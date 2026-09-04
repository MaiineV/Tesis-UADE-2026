using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Items.Active;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Feature#0084 §A1/§A2: cortes custom, binario por paridad, gradiente/jerarquia y
    /// que el modelo legacy (tercios, Precision, Control) siga intacto.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemResolutionTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // Cortes custom — Probability Drive D4 1 / 2-3 / 4
        // ------------------------------------------------------------------

        [TestCase(1, ActiveItemBand.Negative)]
        [TestCase(2, ActiveItemBand.Mixed)]
        [TestCase(3, ActiveItemBand.Mixed)]
        [TestCase(4, ActiveItemBand.Positive)]
        public void test_resolveCuts_d4_customCuts_matchesProbabilityDriveRanges(int roll, ActiveItemBand expected)
        {
            // Arrange + Act
            var band = ActiveItemBands.ResolveCuts(roll, faces: 4, negMaxFace: 1, mixedMaxFace: 3);

            // Assert
            Assert.AreEqual(expected, band);
        }

        [Test]
        public void test_resolveCuts_zeroCuts_fallsBackToThirds()
        {
            // Arrange — 0/0 = "sin cortes custom", tercios de siempre.
            Assert.AreEqual(ActiveItemBand.Negative, ActiveItemBands.ResolveCuts(1, 6, 0, 0));
            Assert.AreEqual(ActiveItemBand.Mixed, ActiveItemBands.ResolveCuts(3, 6, 0, 0));
            Assert.AreEqual(ActiveItemBand.Positive, ActiveItemBands.ResolveCuts(6, 6, 0, 0));
        }

        // ------------------------------------------------------------------
        // Tercios de doc — D10 1-3/4-7/8-10, D12 1-4/5-8/9-12
        // ------------------------------------------------------------------

        [TestCase(1, ActiveItemBand.Negative)]
        [TestCase(3, ActiveItemBand.Negative)]
        [TestCase(4, ActiveItemBand.Mixed)]
        [TestCase(7, ActiveItemBand.Mixed)]
        [TestCase(8, ActiveItemBand.Positive)]
        [TestCase(10, ActiveItemBand.Positive)]
        public void test_resolve_d10_docRanges_needCustomCuts(int roll, ActiveItemBand expected)
        {
            // Los tercios puros de D10 dan 1-3 / 4-6 / 7-10; el doc de Blood Transfusion pide
            // 1-3 / 4-7 / 8-10, asi que ese item se autora con cortes propios 3 / 7.
            Assert.AreEqual(expected, ActiveItemBands.ResolveCuts(roll, DiceType.D10.MaxFace(), 3, 7));
        }

        [Test]
        public void test_resolve_d10_pureThirds_differFromDoc_onFace7()
        {
            Assert.AreEqual(ActiveItemBand.Positive, ActiveItemBands.Resolve(7, DiceType.D10.MaxFace()));
        }

        [TestCase(1, ActiveItemBand.Negative)]
        [TestCase(4, ActiveItemBand.Negative)]
        [TestCase(5, ActiveItemBand.Mixed)]
        [TestCase(8, ActiveItemBand.Mixed)]
        [TestCase(9, ActiveItemBand.Positive)]
        [TestCase(12, ActiveItemBand.Positive)]
        public void test_resolve_d12_thirds_matchDocRanges(int roll, ActiveItemBand expected)
        {
            Assert.AreEqual(expected, ActiveItemBands.Resolve(roll, DiceType.D12.MaxFace()));
        }

        // ------------------------------------------------------------------
        // Validate
        // ------------------------------------------------------------------

        [Test]
        public void test_validate_bandsWithoutCustomCuts_isValid()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);

            Assert.IsTrue(ActiveItemBands.Validate(item, out var error));
            Assert.IsNull(error);
        }

        [Test]
        public void test_validate_bandsWithGoodCustomCuts_isValid()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Bands);
            item.NegativeMaxFace = 1;
            item.MixedMaxFace = 3;

            Assert.IsTrue(ActiveItemBands.Validate(item, out var error));
        }

        [Test]
        public void test_validate_bandsWithCutsOutOfOrder_isRejected()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);
            item.NegativeMaxFace = 4;
            item.MixedMaxFace = 2;

            Assert.IsFalse(ActiveItemBands.Validate(item, out var error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void test_validate_bandsWithCutAtFaces_isRejected()
        {
            // MixedMaxFace == Faces deja la banda positiva vacia.
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);
            item.NegativeMaxFace = 2;
            item.MixedMaxFace = 6;

            Assert.IsFalse(ActiveItemBands.Validate(item, out var error));
        }

        [Test]
        public void test_validate_binaryWithOddFaces_isRejected()
        {
            var item = NewItem(DiceType.D3, ActiveItemResolution.Binary);

            Assert.IsFalse(ActiveItemBands.Validate(item, out var error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void test_validate_binaryWithEvenFaces_isValid()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Binary);

            Assert.IsTrue(ActiveItemBands.Validate(item, out var error));
        }

        [TestCase(ActiveItemResolution.Gradient)]
        [TestCase(ActiveItemResolution.Hierarchy)]
        public void test_validate_gradientAndHierarchy_alwaysValid(ActiveItemResolution structure)
        {
            var item = NewItem(DiceType.D6, structure);

            Assert.IsTrue(ActiveItemBands.Validate(item, out var error));
        }

        // ------------------------------------------------------------------
        // Binary por paridad
        // ------------------------------------------------------------------

        [TestCase(1, ActiveItemParity.Even, ActiveItemBand.Negative)]
        [TestCase(2, ActiveItemParity.Even, ActiveItemBand.Positive)]
        [TestCase(3, ActiveItemParity.Even, ActiveItemBand.Negative)]
        [TestCase(4, ActiveItemParity.Even, ActiveItemBand.Positive)]
        [TestCase(1, ActiveItemParity.Odd, ActiveItemBand.Positive)]
        [TestCase(2, ActiveItemParity.Odd, ActiveItemBand.Negative)]
        public void test_resolveBinary_matchesParity(int roll, ActiveItemParity positiveParity, ActiveItemBand expected)
        {
            Assert.AreEqual(expected, ActiveItemBands.ResolveBinary(roll, faces: 4, positiveParity));
        }

        [Test]
        public void test_resolve_binaryItem_neverReturnsMixed()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Binary);
            item.BinaryPositiveParity = ActiveItemParity.Even;

            for (int roll = 1; roll <= 4; roll++)
                Assert.AreNotEqual(ActiveItemBand.Mixed, ActiveItemBands.Resolve(roll, item));
        }

        [Test]
        public void test_getEffectsFor_binaryItem_dispatchesToNegativeAndPositiveOnly()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Binary);
            item.BinaryPositiveParity = ActiveItemParity.Even;

            var negResolution = ActiveItemBands.ResolveRoll(1, item); // impar -> Negative
            var posResolution = ActiveItemBands.ResolveRoll(2, item); // par -> Positive

            Assert.AreSame(item.OnNegativeBand, item.GetEffectsFor(negResolution));
            Assert.AreSame(item.OnPositiveBand, item.GetEffectsFor(posResolution));
        }

        // ------------------------------------------------------------------
        // Gradient / Hierarchy — Magnitude == Face, un solo grupo
        // ------------------------------------------------------------------

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        public void test_resolveRoll_gradient_magnitudeEqualsFace(int roll)
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Gradient);

            var resolution = ActiveItemBands.ResolveRoll(roll, item);

            Assert.AreEqual(roll, resolution.Face);
            Assert.AreEqual(roll, resolution.Magnitude);
        }

        [TestCase(1)]
        [TestCase(4)]
        public void test_resolveRoll_hierarchy_magnitudeEqualsFace(int roll)
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Hierarchy);

            var resolution = ActiveItemBands.ResolveRoll(roll, item);

            Assert.AreEqual(roll, resolution.Magnitude);
        }

        [TestCase(ActiveItemResolution.Bands)]
        [TestCase(ActiveItemResolution.Binary)]
        public void test_resolveRoll_bandsAndBinary_magnitudeIsAlwaysZero(ActiveItemResolution structure)
        {
            var item = NewItem(DiceType.D6, structure);

            for (int roll = 1; roll <= 6; roll++)
                Assert.AreEqual(0, ActiveItemBands.ResolveRoll(roll, item).Magnitude);
        }

        [Test]
        public void test_getEffectsFor_gradient_alwaysReturnsOnPositiveBand()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Gradient);

            for (int roll = 1; roll <= 6; roll++)
            {
                var resolution = ActiveItemBands.ResolveRoll(roll, item);
                Assert.AreSame(item.OnPositiveBand, item.GetEffectsFor(resolution));
            }
        }

        [Test]
        public void test_getEffectsFor_hierarchy_alwaysReturnsOnPositiveBand()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Hierarchy);

            for (int roll = 1; roll <= 4; roll++)
            {
                var resolution = ActiveItemBands.ResolveRoll(roll, item);
                Assert.AreSame(item.OnPositiveBand, item.GetEffectsFor(resolution));
            }
        }

        // ------------------------------------------------------------------
        // ResolveRoll con raw distinto del ajustado (encantamiento)
        // ------------------------------------------------------------------

        [Test]
        public void test_resolveRoll_withRawAndAdjusted_keepsBothFaces()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);

            var resolution = ActiveItemBands.ResolveRoll(rawRoll: 2, roll: 5, item);

            Assert.AreEqual(2, resolution.RawFace);
            Assert.AreEqual(5, resolution.Face);
            Assert.AreEqual(ActiveItemBand.Positive, resolution.Band, "la banda la decide la cara ajustada");
        }

        // ------------------------------------------------------------------
        // Legacy: tercios simples, Precision y Control intactos
        // ------------------------------------------------------------------

        [Test]
        public void test_resolve_legacyBandsItem_stillUsesThirds()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);

            Assert.AreEqual(ActiveItemBand.Negative, ActiveItemBands.Resolve(1, item));
            Assert.AreEqual(ActiveItemBand.Mixed, ActiveItemBands.Resolve(3, item));
            Assert.AreEqual(ActiveItemBand.Positive, ActiveItemBands.Resolve(6, item));
        }

        [Test]
        public void test_resolve_precisionFamily_stillUsesDistanceToTarget()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);
            item.ActiveFamily = ActiveItemFamily.Precision;
            item.PrecisionTarget = 4;

            Assert.AreEqual(ActiveItemBand.Positive, ActiveItemBands.Resolve(4, item));
            Assert.AreEqual(ActiveItemBand.Mixed, ActiveItemBands.Resolve(3, item));
            Assert.AreEqual(ActiveItemBand.Negative, ActiveItemBands.Resolve(1, item));
        }

        [Test]
        public void test_resolve_controlFamily_stillCrossesParityAndUpperHalf()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);
            item.ActiveFamily = ActiveItemFamily.Control;
            item.ControlParity = ActiveItemParity.Even;

            Assert.AreEqual(ActiveItemBand.Positive, ActiveItemBands.Resolve(6, item), "par y mitad superior");
            Assert.AreEqual(ActiveItemBand.Mixed, ActiveItemBands.Resolve(2, item), "par pero mitad inferior");
            Assert.AreEqual(ActiveItemBand.Negative, ActiveItemBands.Resolve(1, item), "impar y mitad inferior");
        }

        // ------------------------------------------------------------------
        // DescribeStructure
        // ------------------------------------------------------------------

        [Test]
        public void test_describeStructure_bands_returnsThreeRows()
        {
            var item = NewItem(DiceType.D6, ActiveItemResolution.Bands);

            var rows = ActiveItemBands.DescribeStructure(item);

            Assert.AreEqual(3, rows.Count);
        }

        [Test]
        public void test_describeStructure_binary_returnsTwoRows()
        {
            var item = NewItem(DiceType.D4, ActiveItemResolution.Binary);

            var rows = ActiveItemBands.DescribeStructure(item);

            Assert.AreEqual(2, rows.Count);
        }

        [TestCase(ActiveItemResolution.Gradient)]
        [TestCase(ActiveItemResolution.Hierarchy)]
        public void test_describeStructure_gradientAndHierarchy_returnsOneRow(ActiveItemResolution structure)
        {
            var item = NewItem(DiceType.D6, structure);

            var rows = ActiveItemBands.DescribeStructure(item);

            Assert.AreEqual(1, rows.Count);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ItemSO NewItem(DiceType die, ActiveItemResolution resolution)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.test";
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.ActiveResolution = resolution;
            item.OnNegativeBand = new Rollgeon.Effects.EffectData();
            item.OnMixedBand = new Rollgeon.Effects.EffectData();
            item.OnPositiveBand = new Rollgeon.Effects.EffectData();
            _spawned.Add(item);
            return item;
        }
    }
}
