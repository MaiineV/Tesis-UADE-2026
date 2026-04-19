using System;
using NUnit.Framework;
using Rollgeon.Effects.Stubs;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
using UnityEngine;

namespace Rollgeon.Entities.Bosses.Tests
{
    /// <summary>
    /// Tests de <see cref="BossEnergyBuildupBehavior"/>: energia crece, capea, y el flag
    /// <see cref="BossEnergyBuildupBehavior.IsEnergyFull"/> es consistente con el max del SO.
    /// </summary>
    [TestFixture]
    public class BossEnergyBuildupBehaviorTests
    {
        private BossFloorManagerSO _bossSO;

        [SetUp]
        public void SetUp()
        {
            _bossSO = ScriptableObject.CreateInstance<BossFloorManagerSO>();
            _bossSO.BossEnergyMax = 3;
            _bossSO.BossEnergyGainPerTurn = 1;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_bossSO);
        }

        private sealed class TestCtx : BehaviorContext { }

        private BehaviorContext Ctx()
            => new TestCtx { SourceEntity = new Entity { Guid = Guid.NewGuid() } };

        [Test]
        public void Execute_GainsOnePerTurn_UpToCap()
        {
            var b = new BossEnergyBuildupBehavior { BossDataOverride = _bossSO };
            var c = Ctx();

            b.Execute(c); Assert.AreEqual(1, b.CurrentEnergy);
            b.Execute(c); Assert.AreEqual(2, b.CurrentEnergy);
            b.Execute(c); Assert.AreEqual(3, b.CurrentEnergy);
            b.Execute(c); Assert.AreEqual(3, b.CurrentEnergy, "Cap a BossEnergyMax.");
        }

        [Test]
        public void IsEnergyFull_TracksMax()
        {
            var b = new BossEnergyBuildupBehavior { BossDataOverride = _bossSO };
            var c = Ctx();

            Assert.IsFalse(b.IsEnergyFull);
            b.Execute(c); b.Execute(c);
            Assert.IsFalse(b.IsEnergyFull);
            b.Execute(c);
            Assert.IsTrue(b.IsEnergyFull);
        }

        [Test]
        public void Execute_WithoutSO_Noop()
        {
            var b = new BossEnergyBuildupBehavior { BossDataOverride = null };
            Assert.DoesNotThrow(() => b.Execute(Ctx()));
            Assert.AreEqual(0, b.CurrentEnergy);
        }

        [Test]
        public void Execute_CustomGainPerTurn()
        {
            _bossSO.BossEnergyGainPerTurn = 2;
            var b = new BossEnergyBuildupBehavior { BossDataOverride = _bossSO };
            var c = Ctx();

            b.Execute(c); Assert.AreEqual(2, b.CurrentEnergy);
            b.Execute(c); Assert.AreEqual(3, b.CurrentEnergy, "Clamped to Max=3.");
        }
    }
}
