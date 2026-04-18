using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using UnityEngine;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Tests de <see cref="EnemyDataSO"/>: los 4 campos del stub T97b siguen presentes,
    /// las nuevas stats se exponen, <see cref="EnemyDataSO.CreateRuntimeStats"/> produce
    /// un bag con Health/Speed/Energy/HealStrength, y <c>CreateRuntimeBehaviors</c>
    /// clona los behaviors polimorficos.
    /// </summary>
    [TestFixture]
    public class EnemyDataSOTests
    {
        private EnemyDataSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<EnemyDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_so);
        }

        [Test]
        public void T97bFields_ArePreserved()
        {
            // Compile-time check: los campos siguen existiendo con el mismo nombre.
            _so.EntityId = "enemy.support.auditor";
            _so.DisplayName = "Auditor de Mesa";
            _so.WeaknessComboId = "combo.par";
            _so.WeaknessMultiplierOverride = 1.5f;

            Assert.AreEqual("enemy.support.auditor", _so.EntityId);
            Assert.AreEqual("Auditor de Mesa", _so.DisplayName);
            Assert.AreEqual("combo.par", _so.WeaknessComboId);
            Assert.AreEqual(1.5f, _so.WeaknessMultiplierOverride);
        }

        [Test]
        public void NewStatFields_AreExposed_WithDefaults()
        {
            Assert.AreEqual(20, _so.BaseHP);
            Assert.AreEqual(0, _so.BaseAttack);
            Assert.AreEqual(5, _so.BaseHealStrength);
            Assert.AreEqual(4, _so.BaseSpeed);
            Assert.AreEqual(3, _so.MaxEnergy);
        }

        [Test]
        public void CreateRuntimeStats_ContainsHealth_Speed_Energy_HealStrength()
        {
            _so.BaseHP = 18;
            _so.BaseSpeed = 2;
            _so.MaxEnergy = 3;
            _so.BaseHealStrength = 4;

            var attrs = _so.CreateRuntimeStats();
            Assert.IsNotNull(attrs);
            Assert.AreEqual(18, attrs.GetAttributeValue<Health, int>());
            Assert.AreEqual(2, attrs.GetAttributeValue<Speed, int>());
            Assert.AreEqual(3, attrs.GetAttributeValue<Energy, int>());
            Assert.AreEqual(4, attrs.GetAttributeValue<HealStrength, int>());
        }

        [Test]
        public void CreateRuntimeStats_ReturnsFreshInstance_EachCall()
        {
            var a = _so.CreateRuntimeStats();
            var b = _so.CreateRuntimeStats();
            Assert.AreNotSame(a, b);

            // Mutar uno no impacta al otro — stats son independientes.
            a.SetAttributeValue<Health, int>(1);
            Assert.AreEqual(_so.BaseHP, b.GetAttributeValue<Health, int>());
        }

        [Test]
        public void CreateRuntimeBehaviors_ClonesPolymorphicBehaviors()
        {
            var template = new SupportHealBehavior { BaseHealAmount = 9 };
            _so.Behaviors.Add(template);

            var runtime = _so.CreateRuntimeBehaviors();
            Assert.AreEqual(1, runtime.Count);
            var clone = runtime[0] as SupportHealBehavior;
            Assert.IsNotNull(clone, "Deep clone debe preservar el tipo concreto.");
            Assert.AreEqual(9, clone.BaseHealAmount);
            Assert.AreNotSame(template, clone);
        }

        [Test]
        public void CreateRuntimeBehaviors_EmptyList_ReturnsEmpty()
        {
            Assert.AreEqual(0, _so.CreateRuntimeBehaviors().Count);
        }
    }
}
