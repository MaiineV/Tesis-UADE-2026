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

            var result = svc.ValidateApply(0, null);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ValidateApply_OutOfRangeBagIndex_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");

            var result = svc.ValidateApply(bagIndex: 99, ench);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Bag index", result.ErrorMessage);
        }

        [Test]
        public void ValidateApply_IncompatibleDiceType_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("only_d20", allowedTypes: DiceType.D20);

            var result = svc.ValidateApply(0, ench);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("no es compatible", result.ErrorMessage);
        }

        [Test]
        public void ValidateApply_EmptyAllowedDiceTypes_AcceptsAny()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("universal"); // empty AllowedDiceTypes

            var result = svc.ValidateApply(0, ench);

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

            var result = svc.ValidateApply(0, ench);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, result.ProjectedFaces);
        }

        [Test]
        public void ValidateApply_ComposedWithExistingFilter_EmptyIntersection_Fails()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));

            // Apply primer encantamiento (solo pares). Append-only: la proyección de
            // cualquier nuevo encantamiento compone sobre TODOS los existentes — no
            // hay "slot" que reemplazar ni excluir.
            var evens = MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even });
            var apply1 = svc.Apply(0, evens);
            Assert.IsTrue(apply1.Success);

            // Intentar sumar "solo impares" — intersección con lo ya aplicado da vacío.
            var odds = MakeEnchantment("odds",
                filter: new ParityFilter { Allowed = Parity.Odd });

            var result = svc.ValidateApply(0, odds);

            Assert.IsFalse(result.Success);
        }

        // ---- Apply ----------------------------------------------------------

        [Test]
        public void Apply_ValidEnchantment_PersistsInBag()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");

            var result = svc.Apply(0, ench);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.AppliedSlotIndex);
            Assert.AreSame(ench, svc.Bag.GetEnchantmentAt(0, 0));
        }

        [Test]
        public void Apply_CalledTwice_BothEnchantmentsPersist()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var first = MakeEnchantment("first");
            var second = MakeEnchantment("second");

            var result1 = svc.Apply(0, first);
            var result2 = svc.Apply(0, second);

            Assert.IsTrue(result1.Success);
            Assert.AreEqual(0, result1.AppliedSlotIndex);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(1, result2.AppliedSlotIndex);
            Assert.AreEqual(2, svc.Bag.GetEnchantmentCount(0));
            Assert.AreSame(first, svc.Bag.GetEnchantmentAt(0, 0));
            Assert.AreSame(second, svc.Bag.GetEnchantmentAt(0, 1));
        }

        [Test]
        public void Apply_ThreeEnchantments_AllPersistWithoutLosingPrevious()
        {
            // DoD: el bag no tiene techo — un dado acumula encantamientos sin límite
            // y ninguno de los previos se pierde al sumar uno nuevo.
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var first = MakeEnchantment("first");
            var second = MakeEnchantment("second");
            var third = MakeEnchantment("third");

            svc.Apply(0, first);
            svc.Apply(0, second);
            var result3 = svc.Apply(0, third);

            Assert.IsTrue(result3.Success);
            Assert.AreEqual(2, result3.AppliedSlotIndex);
            Assert.AreEqual(3, svc.Bag.GetEnchantmentCount(0));
            Assert.AreSame(first, svc.Bag.GetEnchantmentAt(0, 0));
            Assert.AreSame(second, svc.Bag.GetEnchantmentAt(0, 1));
            Assert.AreSame(third, svc.Bag.GetEnchantmentAt(0, 2));
        }

        [Test]
        public void Remove_ExistingSlot_ClearsAndReturnsTrue()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");
            svc.Apply(0, ench);

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

        [Test]
        public void Remove_IsIdempotent_SecondCallReturnsFalse()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var ench = MakeEnchantment("e");
            svc.Apply(0, ench);
            svc.Remove(0, 0);

            bool removedAgain = svc.Remove(0, 0);

            Assert.IsFalse(removedAgain);
        }

        [Test]
        public void Remove_LeavesOtherEnchantmentIndicesIntact()
        {
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D6));
            var first = MakeEnchantment("first");
            var second = MakeEnchantment("second");
            svc.Apply(0, first);
            svc.Apply(0, second);

            bool removed = svc.Remove(0, 0);

            Assert.IsTrue(removed);
            Assert.IsNull(svc.Bag.GetEnchantmentAt(0, 0));
            Assert.AreSame(second, svc.Bag.GetEnchantmentAt(0, 1),
                "remove tombstonea el slot — no compacta ni corre los índices de los demás");
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
            svc.Apply(0, MakeEnchantment("evens",
                filter: new ParityFilter { Allowed = Parity.Even }));

            var faces = svc.ComputeAllowedFaces(0);

            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, faces);
        }

        [Test]
        public void ComputeAllowedFaces_WithMinHalfMaxFilter_OnD4_ReturnsUpperHalfRoundedUp()
        {
            // BUG-030b: Afilado ahora restringe caras en vez de compensar con bonus post-roll.
            var svc = MakeService();
            svc.InitializeFromBag(MakeBag(DiceType.D4));
            svc.Apply(0, MakeEnchantment("afilado",
                filter: new MinHalfMaxFilter()));

            var faces = svc.ComputeAllowedFaces(0);

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, faces);
        }
    }
}
