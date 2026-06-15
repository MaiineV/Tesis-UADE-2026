using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ContractModifierService"/> (Sistemas prerequisito Bosses §4):
    /// los tres tipos de modificador (Multiply R01/R02, Forbid R03, SetToNeighbor R04/R05),
    /// stacking de N modificadores, y que ClearAll devuelve el Contrato a sus valores base.
    /// </summary>
    [TestFixture]
    public class ContractModifierServiceTests
    {
        private ContractModifierService _svc;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _svc = new ContractModifierService();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
        }

        // --- helpers ---------------------------------------------------------
        private BaseComboSO MakeCombo(string id, int baseDamage)
        {
            var combo = ScriptableObject.CreateInstance<Combo_Par>();
            _created.Add(combo);
            typeof(BaseComboSO).GetField("_comboId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(combo, id);
            typeof(BaseComboSO).GetField("_baseDamage", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(combo, baseDamage);
            return combo;
        }

        private void InjectSheet(params BaseComboSO[] combos)
        {
            var sheet = new ContractSheet { Combos = new List<BaseComboSO>(combos) };
            _svc.ConfigureForTests(() => sheet);
        }

        // --- R01 / R02: multiply ---------------------------------------------
        [Test]
        public void MultiplyCombo_R01_DoublesBaseDamage()
        {
            _svc.MultiplyCombo("combo.par", 2f);

            Assert.AreEqual(20, _svc.GetEffectiveBaseDamage("combo.par", 10));
        }

        [Test]
        public void MultiplyCombo_R02_HalvesBaseDamage()
        {
            _svc.MultiplyCombo("combo.par", 0.5f);

            Assert.AreEqual(5, _svc.GetEffectiveBaseDamage("combo.par", 10));
        }

        [Test]
        public void MultiplyCombo_Stacks_WhenAppliedTwice()
        {
            _svc.MultiplyCombo("combo.par", 2f);
            _svc.MultiplyCombo("combo.par", 2f);

            Assert.AreEqual(40, _svc.GetEffectiveBaseDamage("combo.par", 10));
        }

        // --- R03: forbid -----------------------------------------------------
        [Test]
        public void ForbidCombo_R03_YieldsZeroDamage()
        {
            _svc.ForbidCombo("combo.par");

            Assert.IsTrue(_svc.IsForbidden("combo.par"));
            Assert.AreEqual(0, _svc.GetEffectiveBaseDamage("combo.par", 50));
        }

        // --- R04 / R05: set to neighbor (by base damage) ---------------------
        [Test]
        public void SetComboToNeighbor_R04_RaisesToImmediatelyHigherBase()
        {
            InjectSheet(MakeCombo("combo.par", 5), MakeCombo("combo.trio", 12), MakeCombo("combo.poker", 30));

            _svc.SetComboToNeighbor("combo.par", +1);

            Assert.AreEqual(12, _svc.GetEffectiveBaseDamage("combo.par", 5));
        }

        [Test]
        public void SetComboToNeighbor_R05_LowersToImmediatelyLowerBase()
        {
            InjectSheet(MakeCombo("combo.par", 5), MakeCombo("combo.trio", 12), MakeCombo("combo.poker", 30));

            _svc.SetComboToNeighbor("combo.poker", -1);

            Assert.AreEqual(12, _svc.GetEffectiveBaseDamage("combo.poker", 30));
        }

        [Test]
        public void SetComboToNeighbor_AtExtreme_IsNoop()
        {
            InjectSheet(MakeCombo("combo.par", 5), MakeCombo("combo.trio", 12));

            _svc.SetComboToNeighbor("combo.par", -1); // ya es el mínimo

            Assert.AreEqual(5, _svc.GetEffectiveBaseDamage("combo.par", 5));
        }

        // --- unmodified / clear ----------------------------------------------
        [Test]
        public void GetEffectiveBaseDamage_UnmodifiedCombo_ReturnsBase()
        {
            Assert.AreEqual(13, _svc.GetEffectiveBaseDamage("combo.escalera", 13));
        }

        [Test]
        public void ClearAll_RestoresBaseValues()
        {
            _svc.MultiplyCombo("combo.par", 2f);
            _svc.ForbidCombo("combo.trio");

            _svc.ClearAll();

            Assert.IsFalse(_svc.HasAnyModifier);
            Assert.AreEqual(10, _svc.GetEffectiveBaseDamage("combo.par", 10));
            Assert.AreEqual(8, _svc.GetEffectiveBaseDamage("combo.trio", 8));
        }
    }
}
