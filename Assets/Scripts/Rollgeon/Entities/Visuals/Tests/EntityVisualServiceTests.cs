using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    [TestFixture]
    public class EntityVisualServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private GameObject _heroPrefab;
        private GameObject _enemyPrefab;
        private GameObject _bossPrefab;
        private EntityVisualService _service;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(GridSnapshot.Rect(5, 5));
            _movement = new MovementService(_grid);

            _heroPrefab = MakePrefab("HeroPrefab");
            _enemyPrefab = MakePrefab("EnemyPrefab");
            _bossPrefab = MakePrefab("BossPrefab");

            _service = new EntityVisualService(_grid, _movement, _heroPrefab, _enemyPrefab, _bossPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private GameObject MakePrefab(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.AddComponent<EntityPawn>();
            _created.Add(go);
            return go;
        }

        [Test]
        public void SpawnHero_RegistersPawnAtCoord()
        {
            var guid = Guid.NewGuid();
            var pawn = _service.SpawnHero(guid, null, new GridCoord(1, 2));
            _created.Add(pawn.gameObject);

            Assert.IsNotNull(pawn);
            Assert.AreEqual(guid, pawn.EntityGuid);
            Assert.AreEqual(EntityPawn.PawnKind.Hero, pawn.Kind);
            Assert.IsTrue(_service.TryGetPawn(guid, out var same));
            Assert.AreSame(pawn, same);
            Assert.AreEqual(_grid.GridToWorld(new GridCoord(1, 2)), pawn.transform.position);
        }

        [Test]
        public void SpawnEnemy_UsesBossPrefab_WhenBaseHPHigh()
        {
            var guid = Guid.NewGuid();
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.BaseHP = 100;
            _created.Add(data);

            var pawn = _service.SpawnEnemy(guid, data, new GridCoord(0, 0));
            _created.Add(pawn.gameObject);

            Assert.AreEqual(EntityPawn.PawnKind.Boss, pawn.Kind);
        }

        [Test]
        public void SpawnEnemy_UsesEnemyPrefab_WhenBaseHPLow()
        {
            var guid = Guid.NewGuid();
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.BaseHP = 20;
            _created.Add(data);

            var pawn = _service.SpawnEnemy(guid, data, new GridCoord(0, 0));
            _created.Add(pawn.gameObject);

            Assert.AreEqual(EntityPawn.PawnKind.Enemy, pawn.Kind);
        }

        [Test]
        public void Despawn_RemovesFromLookup_AndDestroysGO()
        {
            var guid = Guid.NewGuid();
            var pawn = _service.SpawnHero(guid, null, GridCoord.Zero);

            _service.Despawn(guid);

            Assert.IsFalse(_service.TryGetPawn(guid, out _));
        }

        [Test]
        public void OnEntityMoved_UpdatesPawnPosition()
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));
            var pawn = _service.SpawnHero(guid, null, new GridCoord(0, 0));
            _created.Add(pawn.gameObject);

            _movement.Move(guid, new GridCoord(3, 0));

            Assert.AreEqual(_grid.GridToWorld(new GridCoord(3, 0)), pawn.transform.position);
        }

        [Test]
        public void TryGetWorldPosition_ReturnsPawnPosition()
        {
            var guid = Guid.NewGuid();
            var pawn = _service.SpawnHero(guid, null, new GridCoord(2, 2));
            _created.Add(pawn.gameObject);

            var pos = _service.TryGetWorldPosition(guid);
            Assert.IsTrue(pos.HasValue);
            Assert.AreEqual(pawn.transform.position, pos.Value);
        }

        [Test]
        public void TryGetWorldPosition_Null_WhenUnregistered()
        {
            Assert.IsNull(_service.TryGetWorldPosition(Guid.NewGuid()));
        }

        [Test]
        public void SpawnHero_Twice_DespawnsPrevious()
        {
            var guid = Guid.NewGuid();
            var first = _service.SpawnHero(guid, null, new GridCoord(0, 0));
            var second = _service.SpawnHero(guid, null, new GridCoord(1, 0));

            Assert.AreNotSame(first, second);
            Assert.IsTrue(_service.TryGetPawn(guid, out var current));
            Assert.AreSame(second, current);
        }

        [Test]
        public void SpawnHero_EmptyGuid_Throws()
        {
            Assert.Throws<ArgumentException>(() => _service.SpawnHero(Guid.Empty, null, GridCoord.Zero));
        }

        [Test]
        public void DespawnAll_ClearsLookup()
        {
            _service.SpawnHero(Guid.NewGuid(), null, GridCoord.Zero);
            _service.SpawnEnemy(Guid.NewGuid(),
                ScriptableObject.CreateInstance<EnemyDataSO>(), new GridCoord(1, 0));

            _service.DespawnAll();
            Assert.IsFalse(_service.TryGetPawn(Guid.NewGuid(), out _));
        }
    }
}
