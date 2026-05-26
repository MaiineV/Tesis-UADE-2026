using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Dice.Filters;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests core de validate/apply del <see cref="DiceEnchantmentService"/>.
    /// No tocan ServiceLocator — usan <see cref="DiceEnchantmentService.InitializeFromBag"/>
    /// directo para popular el <see cref="RuntimeDiceBag"/>.
    /// </summary>
    [TestFixture]
    public class DiceEnchantmentServiceTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---- Helpers --------------------------------------------------------

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>(dice);
            bag.name = "TestBag";
            _created.Add(bag);
            return bag;
        }

        private EnchantmentSO MakeEnchantment(string id, IFaceFilter filter = null, params DiceType[] allowedTypes)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);

            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>(allowedTypes));
            if (filter != null)
            {
                typeof(EnchantmentSO).GetField("_faceFilter", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, filter);
            }
            return ench;
        }

        private DiceEnchantmentService MakeService()
        {
            // Pass null config — validación cae a defaults (min 1 cara).
            return new DiceEnchantmentService(config: null);
        }

        // ---- ValidateApply --------------------------------------------------

        [Test]
        public void ValidateApply_NullEnchantment_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            var result = svc.ValidateApply(0, 0, null);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ValidateApply_OutOfRangeBagIndex_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");

            var result = svc.ValidateApply(bagIndex: 99, enchSlotIndex: 0, ench);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Bag index", result.ErrorMessage);
        }

        [Test]
        public void ValidateApply_OutOfRangeSlotIndex_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6)); // D6 = 2 cupos
            var ench = MakeEnchantment("e");

            var result = svc.ValidateApply(bagIndex: 0, enchSlotIndex: 99, ench);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Slot", result.ErrorMessage);
        }

        [Test]
        public void ValidateApply_IncompatibleDiceType_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("only_d20", allowedTypes: DiceType.D20);

            var result = svc.ValidateApply(0, 0, ench);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("no es compatible", result.ErrorMessage);
        }

        [Test]
        public void ValidateApply_EmptyAllowedDiceTypes_AcceptsAny()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("universal"); // empty AllowedDiceTypes

            var result = svc.ValidateApply(0, 0, ench);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6 }, result.ProjectedFaces);
        }

        [Test]
        public void ValidateApply_WithFaceFilter_PreviewMatchesIntersection()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even });

            var result = svc.ValidateApply(0, 0, ench);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, result.ProjectedFaces);
        }

        [Test]
        public void ValidateApply_TwoFiltersComposeToEmpty_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            // Apply primer enchantment (solo pares) — ocupa slot 0.
            var evens = MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even });
            var apply1 = svc.Apply(0, 0, evens);
            Assert.IsTrue(apply1.Success);

            // Intentar agregar "solo impares" en slot 1 — intersección vacía.
            var odds = MakeEnchantment("odds",
                filter: new ParityFilter { Allowed = Parity.Odd });

            var result = svc.ValidateApply(0, 1, odds);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ValidateApply_ReEnchantSameSlot_IgnoresExistingFilter()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            // Slot 0 = solo pares.
            var evens = MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even });
            svc.Apply(0, 0, evens);

            // Re-enchant slot 0 (mismo slot) con solo impares — debería pasar
            // porque ignoramos el filter del slot que estamos reemplazando.
            var odds = MakeEnchantment("odds",
                filter: new ParityFilter { Allowed = Parity.Odd });

            var result = svc.ValidateApply(0, 0, odds);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEquivalent(new[] { 1, 3, 5 }, result.ProjectedFaces);
        }

        // ---- Apply ----------------------------------------------------------

        [Test]
        public void Apply_ValidEnchantment_PersistsInBag()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");

            var result = svc.Apply(0, 0, ench);

            Assert.IsTrue(result.Success);
            Assert.AreSame(ench, svc.Bag.GetEnchantmentAt(0, 0));
        }

        [Test]
        public void Apply_OverExistingSlot_ReplacesEnchantment()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var first = MakeEnchantment("first");
            var second = MakeEnchantment("second");

            svc.Apply(0, 0, first);
            var result = svc.Apply(0, 0, second);

            Assert.IsTrue(result.Success);
            Assert.AreSame(second, svc.Bag.GetEnchantmentAt(0, 0),
                "re-enchant debe reemplazar el slot");
        }

        [Test]
        public void Remove_ExistingSlot_ClearsAndReturnsTrue()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");
            svc.Apply(0, 0, ench);

            bool removed = svc.Remove(0, 0);

            Assert.IsTrue(removed);
            Assert.IsNull(svc.Bag.GetEnchantmentAt(0, 0));
        }

        [Test]
        public void Remove_EmptySlot_ReturnsFalse()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            bool removed = svc.Remove(0, 0);

            Assert.IsFalse(removed);
        }

        // ---- ComputeAllowedFaces --------------------------------------------

        [Test]
        public void ComputeAllowedFaces_FreshBag_ReturnsAllFaces()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            var faces = svc.ComputeAllowedFaces(0);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6 }, faces);
        }

        [Test]
        public void ComputeAllowedFaces_WithEvensFilter_ReturnsEvenFacesOnly()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            svc.Apply(0, 0, MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even }));

            var faces = svc.ComputeAllowedFaces(0);

            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, faces);
        }
    }
}
