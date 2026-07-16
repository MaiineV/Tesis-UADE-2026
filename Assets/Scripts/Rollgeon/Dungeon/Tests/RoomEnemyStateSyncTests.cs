using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dungeon.State;
using Rollgeon.Grid;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Writeback del estado vivo de enemigos a sus <see cref="EnemySpawnState"/>
    /// (Feature#0028 Fase 2): HP actual (stat runtime), tile de grilla y GUID.
    /// </summary>
    [TestFixture]
    public class RoomEnemyStateSyncTests
    {
        private AttributesManager _attributes;
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attributes = new AttributesManager();
            _grid = new GridManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            ServiceLocator.Clear();
        }

        private Guid RegisterLiveEnemy(int maxHp, int currentHp, GridCoord cell)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(maxHp));
            _attributes.Register(guid, attrs);
            _attributes.SetAttributeValue<Health, int>(guid, currentHp);
            _grid.Register(guid, cell);
            return guid;
        }

        [Test]
        public void SnapshotLiveEnemies_WritesLiveHpPositionAndGuid()
        {
            var guid = RegisterLiveEnemy(maxHp: 20, currentHp: 6, cell: new GridCoord(3, 4));

            var instance = new RoomInstance { InstanceId = Guid.NewGuid() };
            instance.SpawnedEnemies.Add(guid);
            instance.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "e",
                CurrentHP = 99,        // valor stale — debe pisarse con el HP vivo
                IsDead = false,
                SpawnPointIndex = 0,
            });

            RoomEnemyStateSync.SnapshotLiveEnemies(instance);

            Assert.IsTrue(instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var s));
            Assert.AreEqual(6, s.CurrentHP, "HP vivo del stat runtime");
            Assert.IsTrue(s.HasLastCell);
            Assert.AreEqual(new GridCoord(3, 4), s.LastCell);
            Assert.AreEqual(guid.ToString(), s.Guid);
        }

        [Test]
        public void SnapshotLiveEnemies_SkipsDeadStates()
        {
            // Enemigo vivo (index 1) + un state muerto (index 0). SpawnedEnemies solo
            // tiene el vivo — el pareo por SpawnPointIndex debe escribir solo ese.
            var aliveGuid = RegisterLiveEnemy(maxHp: 20, currentHp: 11, cell: new GridCoord(2, 2));

            var instance = new RoomInstance { InstanceId = Guid.NewGuid() };
            instance.SpawnedEnemies.Add(aliveGuid);
            instance.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0", EnemyDataSOId = "e",
                CurrentHP = 0, IsDead = true, SpawnPointIndex = 0,
            });
            instance.ObjectStates.Set("enemy_1", new EnemySpawnState
            {
                SpawnPointId = "enemy_1", EnemyDataSOId = "e",
                CurrentHP = 99, IsDead = false, SpawnPointIndex = 1,
            });

            RoomEnemyStateSync.SnapshotLiveEnemies(instance);

            instance.ObjectStates.TryGet<EnemySpawnState>("enemy_1", out var alive);
            Assert.AreEqual(11, alive.CurrentHP, "el vivo se actualiza");
            Assert.AreEqual(aliveGuid.ToString(), alive.Guid);

            instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var dead);
            Assert.IsFalse(dead.HasLastCell, "el muerto no se toca");
        }

        [Test]
        public void SnapshotLiveEnemies_NoSpawnedEnemies_IsNoop()
        {
            var instance = new RoomInstance { InstanceId = Guid.NewGuid() };
            Assert.DoesNotThrow(() => RoomEnemyStateSync.SnapshotLiveEnemies(instance));
        }
    }
}
