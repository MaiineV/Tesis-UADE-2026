using NUnit.Framework;
using Rollgeon.Combos;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Feature#0055 — tabla de la Habilidad de Clase (Empuje): los 8 valores del GDD, ids sin
    /// entrada ⇒ 0 casillas (la tirada se consume sin efecto) y el daño de choque por default.
    /// </summary>
    [TestFixture]
    public sealed class ClassSkillPushTableSOTests
    {
        private ClassSkillPushTableSO _table;

        [SetUp]
        public void SetUp() => _table = ClassSkillPushTableSO.CreateDefault();

        [TearDown]
        public void TearDown()
        {
            if (_table != null) Object.DestroyImmediate(_table);
        }

        [TestCase(ComboId.Par, 1)]
        [TestCase(ComboId.DoublePair, 1)]
        [TestCase(ComboId.HigherNumber, 2)]
        [TestCase(ComboId.Triple, 2)]
        [TestCase(ComboId.FullHouse, 3)]
        [TestCase(ComboId.Straight, 3)]
        [TestCase(ComboId.Poker, 4)]
        [TestCase(ComboId.Generala, 5)]
        public void GetTiles_SpecCombo_ReturnsGddDistance(string comboId, int expected)
        {
            Assert.AreEqual(expected, _table.GetTiles(comboId));
        }

        [Test]
        public void CreateDefault_HasExactlyEightEntries()
        {
            Assert.AreEqual(8, _table.Entries.Count);
        }

        [TestCase(ComboId.BruteForce)]
        [TestCase("combo.unknown")]
        [TestCase("")]
        [TestCase(null)]
        public void GetTiles_UnlistedOrEmptyId_ReturnsZero(string comboId)
        {
            Assert.AreEqual(0, _table.GetTiles(comboId));
        }

        [Test]
        public void CreateDefault_CollisionDamageIsTen()
        {
            Assert.AreEqual(10, _table.CollisionDamage);
        }

        [Test]
        public void ResetToSpec_AfterMutation_RestoresSpecValues()
        {
            _table.Entries.Clear();
            _table.CollisionDamage = 99;

            _table.ResetToSpec();

            Assert.AreEqual(5, _table.GetTiles(ComboId.Generala));
            Assert.AreEqual(ClassSkillPushTableSO.DefaultCollisionDamage, _table.CollisionDamage);
        }

        [Test]
        public void GetTiles_NegativeTilesEntry_ClampsToZero()
        {
            _table.Entries.Clear();
            _table.Entries.Add(new ClassSkillPushTableSO.Entry(ComboId.Par, -3));

            Assert.AreEqual(0, _table.GetTiles(ComboId.Par));
        }
    }
}
