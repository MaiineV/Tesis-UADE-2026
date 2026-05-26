using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Entities.Visuals.Tests
{
    [TestFixture]
    public class EntityVisualServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private EntityVisualService _service;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _movement = new MovementService(_grid);

            _service = new EntityVisualService(_grid, _movement);
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

        private ClassHeroSO MakeHero(string prefabName)
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.VisualPrefab = MakePrefab(prefabName);
            _created.Add(hero);
            return hero;
        }

        private EnemyDataSO MakeEnemy(string prefabName)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.VisualPrefab = MakePrefab(prefabName);
            _created.Add(data);
            return data;
        }

        [Test]
        public void SpawnHero_RegistersPawnAtCoord()
        {
            // Arrange
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();

            // Act
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(1, 2));
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(guid, pawn.EntityGuid);
            Assert.AreEqual(EntityPawn.PawnKind.Hero, pawn.Kind);
            Assert.IsTrue(_service.TryGetPawn(guid, out var same));
            Assert.AreSame(pawn, same);
            // La colocación en grilla es un asunto XZ; el pawn además se eleva PawnYOffset
            // en Y (lift visual sobre el piso), así que comparamos solo el plano.
            var expected = _grid.GridToWorld(new GridCoord(1, 2));
            Assert.AreEqual(expected.x, pawn.transform.position.x, 1e-4f);
            Assert.AreEqual(expected.z, pawn.transform.position.z, 1e-4f);
        }

        [Test]
        public void SpawnHero_UsesVisualPrefabFromClassHeroSO()
        {
            // Arrange
            var hero = MakeHero("WarriorVisual");

            // Act
            var pawn = _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(EntityPawn.PawnKind.Hero, pawn.Kind);
        }

        [Test]
        public void SpawnHero_LogsErrorAndReturnsNull_WhenVisualPrefabMissing()
        {
            // Arrange
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.VisualPrefab = null;
            _created.Add(hero);

            // Act + Assert
            LogAssert.Expect(LogType.Error, new Regex("no tiene VisualPrefab"));
            var pawn = _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            Assert.IsNull(pawn);
        }

        [Test]
        public void SpawnHero_Throws_WhenHeroIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => _service.SpawnHero(Guid.NewGuid(), null, GridCoord.Zero));
        }

        [Test]
        public void SpawnEnemy_UsesVisualPrefabFromData()
        {
            // Arrange
            var data = MakeEnemy("CustomEnemyVisual");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(EntityPawn.PawnKind.Enemy, pawn.Kind);
        }

        [Test]
        public void SpawnEnemy_LogsErrorAndReturnsNull_WhenVisualPrefabMissing()
        {
            // Arrange
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.VisualPrefab = null;
            _created.Add(data);

            // Act + Assert
            LogAssert.Expect(LogType.Error, new Regex("no tiene VisualPrefab"));
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            Assert.IsNull(pawn);
        }

        [Test]
        public void SpawnEnemy_Throws_WhenDataIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => _service.SpawnEnemy(Guid.NewGuid(), null, GridCoord.Zero));
        }

        [Test]
        public void Despawn_RemovesFromLookup_AndDestroysGO()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            _service.SpawnHero(guid, hero, GridCoord.Zero);

            _service.Despawn(guid);

            Assert.IsFalse(_service.TryGetPawn(guid, out _));
        }

        [Test]
        public void OnEntityMoved_UpdatesPawnPosition()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(0, 0));
            _created.Add(pawn.gameObject);

            _movement.Move(guid, new GridCoord(3, 0));

            // Solo XZ: el PawnYOffset eleva el pawn en Y (lift visual), no afecta la celda.
            var expected = _grid.GridToWorld(new GridCoord(3, 0));
            Assert.AreEqual(expected.x, pawn.transform.position.x, 1e-4f);
            Assert.AreEqual(expected.z, pawn.transform.position.z, 1e-4f);
        }

        [Test]
        public void TryGetWorldPosition_ReturnsPawnPosition()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(2, 2));
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
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            var first = _service.SpawnHero(guid, hero, new GridCoord(0, 0));
            var second = _service.SpawnHero(guid, hero, new GridCoord(1, 0));

            Assert.AreNotSame(first, second);
            Assert.IsTrue(_service.TryGetPawn(guid, out var current));
            Assert.AreSame(second, current);
        }

        [Test]
        public void SpawnHero_EmptyGuid_Throws()
        {
            var hero = MakeHero("HeroPrefab");
            Assert.Throws<ArgumentException>(
                () => _service.SpawnHero(Guid.Empty, hero, GridCoord.Zero));
        }

        [Test]
        public void DespawnAll_ClearsLookup()
        {
            var hero = MakeHero("HeroPrefab");
            var enemy = MakeEnemy("EnemyVisual");

            _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            _service.SpawnEnemy(Guid.NewGuid(), enemy, new GridCoord(1, 0));

            _service.DespawnAll();
            Assert.IsFalse(_service.TryGetPawn(Guid.NewGuid(), out _));
        }
    }
}
