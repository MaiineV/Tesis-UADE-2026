using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_SpawnReinforcements"/>: spawnea refuerzos en tiles del
    /// borde de la sala, los registra en los servicios runtime, y los suma a la cola de
    /// turnos vía <see cref="TurnOrderService.Append"/>.
    /// </summary>
    [TestFixture]
    public class AINode_SpawnReinforcementsTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private InMemoryEntityRegistry _registry;
        private TurnOrderService _turnOrder;
        private EnemyDataSO _enemyToSpawn;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _attributes = new AttributesManager();
            _registry = new InMemoryEntityRegistry();
            _turnOrder = new TurnOrderService();
            ServiceLocator.AddService<InMemoryEntityRegistry>(_registry);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);

            _enemyToSpawn = ScriptableObject.CreateInstance<EnemyDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _attributes.Dispose();
            UnityEngine.Object.DestroyImmediate(_enemyToSpawn);
        }

        private AIContext NewContext(Guid self) => new AIContext
        {
            SelfGuid = self,
            Grid = _grid,
            Attributes = _attributes,
            Rng = new System.Random(1),
        };

        [Test]
        public void Tick_SpawnsExactCount_WhenRoomHasEnoughFreeEdgeTiles()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            var result = node.Tick(NewContext(Guid.NewGuid()));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_SpawnedTilesAreOnRoomPerimeter()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            node.Tick(NewContext(Guid.NewGuid()));

            foreach (var id in _turnOrder.OrderForRound)
            {
                Assert.IsTrue(_grid.TryGetPosition(id, out var coord));
                bool onEdge = coord.X == 0 || coord.X == 4 || coord.Y == 0 || coord.Y == 4;
                Assert.IsTrue(onEdge, $"Tile {coord} no está en el perímetro de la sala 5x5.");
            }
        }

        [Test]
        public void Tick_RegistersSpawnedEntitiesInRegistryAndAttributes()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            node.Tick(NewContext(Guid.NewGuid()));

            foreach (var id in _turnOrder.OrderForRound)
            {
                Assert.IsTrue(_registry.TryGetAttributes(id, out _));
                Assert.IsTrue(_attributes.IsRegistered(id));
            }
        }

        [Test]
        public void Tick_AppendsSpawnedEntities_AfterExistingParticipants_WithoutMovingCursor()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var player = Guid.NewGuid();
            var boss = Guid.NewGuid();
            _turnOrder.RestoreState(new[] { player, boss }, cursor: 1, roundIndex: 0);

            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };
            node.Tick(NewContext(boss));

            Assert.AreEqual(4, _turnOrder.ParticipantCount);
            Assert.AreEqual(player, _turnOrder.OrderForRound[0]);
            Assert.AreEqual(boss, _turnOrder.OrderForRound[1]);
            Assert.AreEqual(boss, _turnOrder.Current, "Append no debe mover el cursor del boss actuando.");
        }

        [Test]
        public void Tick_NullGrid_ReturnsFailed()
        {
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };
            var ctx = new AIContext { SelfGuid = Guid.NewGuid(), Grid = null, Attributes = _attributes };

            Assert.AreEqual(AIResult.Failed, node.Tick(ctx));
            Assert.AreEqual(0, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_EmptyRoomGraph_ReturnsFailed()
        {
            // _grid nunca cargó una sala (grafo vacío) — sin tiles de borde para enumerar.
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            var result = node.Tick(NewContext(Guid.NewGuid()));

            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(0, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_NoEnemyToSpawn_ReturnsFailed()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = null, Count = 2 };

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext(Guid.NewGuid())));
        }

        [Test]
        public void Tick_MissingTurnOrderService_ReturnsFailed()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<InMemoryEntityRegistry>(_registry); // TurnOrderService NO registrado
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext(Guid.NewGuid())));
        }
    }
}
