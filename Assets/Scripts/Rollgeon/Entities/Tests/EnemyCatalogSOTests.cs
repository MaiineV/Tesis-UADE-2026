using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Entities;
using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Tests de <see cref="EnemyCatalogSO"/>: lookup por EntityId, AllIds, ausencia
    /// → null en <see cref="EnemyCatalogSO.GetById"/>.
    /// </summary>
    [TestFixture]
    public class EnemyCatalogSOTests
    {
        private EnemyCatalogSO _catalog;
        private readonly List<EnemyDataSO> _owned = new List<EnemyDataSO>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<EnemyCatalogSO>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var e in _owned) Object.DestroyImmediate(e);
            _owned.Clear();
            Object.DestroyImmediate(_catalog);
        }

        private EnemyDataSO MakeEnemy(string id)
        {
            var e = ScriptableObject.CreateInstance<EnemyDataSO>();
            e.EntityId = id;
            _owned.Add(e);
            return e;
        }

        private void SetEntries(List<EnemyDataSO> entries)
        {
            var field = typeof(BaseCatalogSO<EnemyDataSO>).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(_catalog, entries);
        }

        [Test]
        public void AllIds_ReflectsEntries()
        {
            SetEntries(new List<EnemyDataSO> { MakeEnemy("a"), MakeEnemy("b") });

            var ids = _catalog.AllIds.ToList();
            Assert.Contains("a", ids);
            Assert.Contains("b", ids);
            Assert.AreEqual(2, ids.Count);
        }

        [Test]
        public void GetById_ReturnsEntry_WhenPresent()
        {
            var target = MakeEnemy("enemy.auditor");
            SetEntries(new List<EnemyDataSO> { MakeEnemy("enemy.melee"), target });

            var found = _catalog.GetById("enemy.auditor");
            Assert.AreSame(target, found);
        }

        [Test]
        public void GetById_UnknownId_ReturnsNull()
        {
            SetEntries(new List<EnemyDataSO> { MakeEnemy("a") });
            Assert.IsNull(_catalog.GetById("missing"));
        }

        [Test]
        public void GetById_EmptyOrNullId_ReturnsNull()
        {
            SetEntries(new List<EnemyDataSO> { MakeEnemy("a") });
            Assert.IsNull(_catalog.GetById(null));
            Assert.IsNull(_catalog.GetById(""));
        }

        [Test]
        public void AllIds_SkipsNullEntries()
        {
            SetEntries(new List<EnemyDataSO> { MakeEnemy("a"), null, MakeEnemy("b") });
            var ids = _catalog.AllIds.ToList();
            Assert.AreEqual(2, ids.Count);
        }
    }
}
