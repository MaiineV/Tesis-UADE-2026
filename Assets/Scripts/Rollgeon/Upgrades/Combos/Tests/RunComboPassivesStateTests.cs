using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Cobertura de <see cref="RunComboPassivesState"/> tras los hooks genéricos:
    /// <see cref="RunComboPassivesState.GetAll"/>, el bucket reservado para pasivas
    /// sin <c>TargetComboId</c> (genéricas) y el round-trip de save con genéricas.
    /// </summary>
    [TestFixture]
    public class RunComboPassivesStateTests
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

        private ComboPassiveSO MakePassive(string id, string targetComboId)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            passive.name = id;
            _created.Add(passive);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, id);
            typeof(ComboPassiveSO).GetField("_targetComboId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, targetComboId);
            return passive;
        }

        [Test]
        public void GetAll_ReturnsPassivesAcrossMultipleComboIds()
        {
            var state = new RunComboPassivesState();
            var par = MakePassive("p1", "combo.par");
            var trio = MakePassive("p2", "combo.trio");

            state.Add(par);
            state.Add(trio);

            CollectionAssert.AreEquivalent(new[] { par, trio }, state.GetAll());
        }

        [Test]
        public void GetAll_IncludesGenericPassivesWithEmptyTargetComboId()
        {
            var state = new RunComboPassivesState();
            var targeted = MakePassive("p1", "combo.par");
            var generic = MakePassive("p2", null);

            state.Add(targeted);
            state.Add(generic);

            CollectionAssert.Contains(state.GetAll(), generic,
                "Una pasiva sin TargetComboId debe participar del dispatch genérico.");
            Assert.AreEqual(2, state.TotalCount);
        }

        [Test]
        public void Add_EmptyTargetComboId_NotReturnedByGetPerCombo()
        {
            var state = new RunComboPassivesState();
            state.Add(MakePassive("p1", string.Empty));

            Assert.AreEqual(0, state.Get("combo.par").Count,
                "Las genéricas no tienen afinidad de combo — Get(comboId) no debe verlas.");
            Assert.AreEqual(1, state.TotalCount);
        }

        [Test]
        public void CaptureRestore_RoundTripsGenericPassives()
        {
            var generic = MakePassive("upg.generic", null);
            var targeted = MakePassive("upg.par", "combo.par");
            ComboPassiveSO Resolve(string id) => id == "upg.generic" ? generic : id == "upg.par" ? targeted : null;

            var source = new RunComboPassivesState(Resolve);
            source.Add(generic);
            source.Add(targeted);
            var saved = source.CaptureState();

            var restored = new RunComboPassivesState(Resolve);
            restored.RestoreState(saved);

            Assert.AreEqual(2, restored.TotalCount);
            CollectionAssert.AreEquivalent(new[] { generic, targeted }, restored.GetAll());
            Assert.AreEqual(1, restored.Get("combo.par").Count);
        }
    }
}
