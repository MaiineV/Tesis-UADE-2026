using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Smoke del catalogo con los 8 concretos (plan §9.4):
    /// <c>AllIds.Count() == 8</c>, <c>GetById</c> retorna tipado, <c>Contains</c> reconoce presentes/ausentes.
    /// </summary>
    [TestFixture]
    public class ComboCatalogTests
    {
        private ComboCatalogSO _catalog;
        private List<BaseComboSO> _allCombos;

        [SetUp]
        public void Setup()
        {
            _allCombos = new List<BaseComboSO>
            {
                ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10),
                ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18),
                ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28),
                ComboTestUtils.CreateCombo<Combo_Escalera>(ComboId.Straight, 35),
                ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40),
                ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 60),
                ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100),
                ComboTestUtils.CreateCombo<Combo_SumaX>(ComboId.SumX, 25),
            };

            _catalog = ScriptableObject.CreateInstance<ComboCatalogSO>();
            ComboTestUtils.SetField(_catalog, "_entries", new List<BaseComboSO>(_allCombos));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            foreach (var c in _allCombos)
            {
                if (c != null) Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void Catalog_AllIds_Returns_Eight_Ids()
        {
            var ids = _catalog.AllIds.ToList();
            Assert.AreEqual(8, ids.Count);
            CollectionAssert.Contains(ids, ComboId.Par);
            CollectionAssert.Contains(ids, ComboId.DoublePair);
            CollectionAssert.Contains(ids, ComboId.Triple);
            CollectionAssert.Contains(ids, ComboId.Straight);
            CollectionAssert.Contains(ids, ComboId.FullHouse);
            CollectionAssert.Contains(ids, ComboId.Poker);
            CollectionAssert.Contains(ids, ComboId.Generala);
            CollectionAssert.Contains(ids, ComboId.SumX);
        }

        [Test]
        public void Catalog_GetById_Generala_Returns_Typed_Reference()
        {
            var generala = _catalog.GetById(ComboId.Generala);
            Assert.IsNotNull(generala);
            Assert.IsInstanceOf<Combo_Generala>(generala);
        }

        [Test]
        public void Catalog_GetById_SumX_Returns_SumaX_Instance()
        {
            var sumx = _catalog.GetById(ComboId.SumX);
            Assert.IsNotNull(sumx);
            Assert.IsInstanceOf<Combo_SumaX>(sumx);
        }

        [Test]
        public void Catalog_Contains_Inexistente_Returns_False()
        {
            Assert.IsFalse(_catalog.Contains("combo.inexistente"));
        }

        [Test]
        public void Catalog_Contains_Par_Returns_True()
        {
            Assert.IsTrue(_catalog.Contains(ComboId.Par));
        }

        [Test]
        public void Catalog_GetById_EmptyString_Returns_Default()
        {
            var result = _catalog.GetById(string.Empty);
            Assert.IsNull(result);
        }

        [Test]
        public void Catalog_GetById_Null_Returns_Default()
        {
            var result = _catalog.GetById(null);
            Assert.IsNull(result);
        }

        [Test]
        public void Catalog_CatalogName_Defaults_To_Type_Name()
        {
            Assert.AreEqual(nameof(ComboCatalogSO), _catalog.CatalogName);
        }
    }
}
