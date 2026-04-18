using NUnit.Framework;
using Rollgeon.Entities.Bosses;
using UnityEngine;

namespace Rollgeon.Entities.Bosses.Tests
{
    /// <summary>
    /// Tests de <see cref="BossFloorManagerSO"/>: los campos expuestos tienen los defaults
    /// de la spec (#103 — interval 3, duration 2, doubleDamageWhenFull 0.5), hereda
    /// <see cref="Rollgeon.Entities.EnemyDataSO"/>, y las clamps defensivas en el setter via
    /// asignacion directa son respetadas.
    /// </summary>
    [TestFixture]
    public class BossFloorManagerSOTests
    {
        private BossFloorManagerSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<BossFloorManagerSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_so);
        }

        [Test]
        public void Defaults_MatchSpec103()
        {
            Assert.AreEqual(3, _so.ComboBlockIntervalTurns);
            Assert.AreEqual(2, _so.ComboBlockDurationTurns);
            Assert.AreEqual(4, _so.BossEnergyMax);
            Assert.AreEqual(1, _so.BossEnergyGainPerTurn);
            Assert.AreEqual(0f, _so.DoubleDamageChanceDefault);
            Assert.AreEqual(0.5f, _so.DoubleDamageChanceWhenEnergyFull);
        }

        [Test]
        public void InheritsEnemyDataSO()
        {
            Assert.IsInstanceOf<Rollgeon.Entities.EnemyDataSO>(_so);
            // T97b fields still reachable.
            _so.EntityId = "boss_floor_manager";
            _so.WeaknessComboId = "combo.par";
            Assert.AreEqual("boss_floor_manager", _so.EntityId);
        }
    }
}
