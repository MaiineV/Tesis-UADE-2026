using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Verifica que <see cref="ContractWarriorFactory"/> respete el orden canonico del GD §5.4.
    /// </summary>
    [TestFixture]
    public class ContractWarriorFactoryTests
    {
        private ComboCatalogSO _catalog;
        private readonly List<BaseComboSO> _owned = new List<BaseComboSO>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<ComboCatalogSO>();

            var par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            var doblePar = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
            var sumaX = ComboTestUtils.CreateCombo<Combo_SumaX>(ComboId.SumX, 25);
            var trio = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);
            var escalera = ComboTestUtils.CreateCombo<Combo_Escalera>(ComboId.Straight, 35);
            var fullHouse = ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40);
            var poker = ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 60);
            var generala = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);

            _owned.AddRange(new BaseComboSO[] { par, doblePar, sumaX, trio, escalera, fullHouse, poker, generala });

            // Inject entries via reflection — BaseCatalogSO<T>._entries is protected.
            var entriesField = typeof(Rollgeon.Patterns.Catalogs.BaseCatalogSO<BaseComboSO>).GetField(
                "_entries",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            entriesField.SetValue(_catalog, new List<BaseComboSO>(_owned));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in _owned) Object.DestroyImmediate(c);
            _owned.Clear();
            Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void CanonicalOrder_Matches_GD_54()
        {
            Assert.AreEqual(ComboId.Par, ContractWarriorFactory.CanonicalOrder[0]);
            Assert.AreEqual(ComboId.DoublePair, ContractWarriorFactory.CanonicalOrder[1]);
            Assert.AreEqual(ComboId.SumX, ContractWarriorFactory.CanonicalOrder[2]);
            Assert.AreEqual(ComboId.Triple, ContractWarriorFactory.CanonicalOrder[3]);
            Assert.AreEqual(ComboId.Straight, ContractWarriorFactory.CanonicalOrder[4]);
            Assert.AreEqual(ComboId.FullHouse, ContractWarriorFactory.CanonicalOrder[5]);
            Assert.AreEqual(ComboId.Poker, ContractWarriorFactory.CanonicalOrder[6]);
            Assert.AreEqual(ComboId.Generala, ContractWarriorFactory.CanonicalOrder[7]);
            Assert.AreEqual(8, ContractWarriorFactory.CanonicalOrder.Count);
        }

        [Test]
        public void Build_Produces_Sheet_With_Canonical_Order()
        {
            var sheet = ContractWarriorFactory.Build(_catalog);
            Assert.AreEqual(8, sheet.Combos.Count);
            for (int i = 0; i < ContractWarriorFactory.CanonicalOrder.Count; i++)
            {
                Assert.AreEqual(ContractWarriorFactory.CanonicalOrder[i], sheet.Combos[i].ComboId,
                    $"Entry [{i}] must be {ContractWarriorFactory.CanonicalOrder[i]}.");
            }
        }

        [Test]
        public void Build_Produces_Sheet_That_Validates()
        {
            var sheet = ContractWarriorFactory.Build(_catalog);
            bool ok = sheet.Validate(out var error);
            Assert.IsTrue(ok, $"Warrior sheet must validate. Error: {error}");
        }

        [Test]
        public void Build_Sets_DisplayLabel()
        {
            var sheet = ContractWarriorFactory.Build(_catalog, "TestLabel");
            Assert.AreEqual("TestLabel", sheet.DisplayLabel);
        }
    }
}
