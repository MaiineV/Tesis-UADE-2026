using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.Serialization;
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
        public void Tick_MultiCellReinforcement_RegistersWholeRectangleOnEdge()
        {
            // Fase C: el edge-picking usa CanPlace — el 2×2 entra completo, sin fallback 1×1.
            _grid.LoadRoom(NavGraph.Rect(7, 7));
            _enemyToSpawn.Footprint = new Vector2Int(2, 2);
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            var result = node.Tick(NewContext(Guid.NewGuid()));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, _turnOrder.ParticipantCount);
            foreach (var id in _turnOrder.OrderForRound)
            {
                Assert.AreEqual(new Vector2Int(2, 2), _grid.GetFootprint(id),
                    "el refuerzo tiene que registrarse con su footprint, no 1×1");
                Assert.IsTrue(_grid.TryGetPosition(id, out var anchor));
                bool touchesEdge = anchor.X == 0 || anchor.X + 1 == 6 || anchor.Y == 0 || anchor.Y + 1 == 6;
                Assert.IsTrue(touchesEdge, $"El rect anclado en {anchor} no toca el perímetro de la sala 7x7.");
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
        public void Tick_TwoReinforcements_SpawnFarApart_NotStuckTogether()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            node.Tick(NewContext(Guid.NewGuid()));

            var coords = new System.Collections.Generic.List<GridCoord>();
            foreach (var id in _turnOrder.OrderForRound)
            {
                Assert.IsTrue(_grid.TryGetPosition(id, out var coord));
                coords.Add(coord);
            }

            Assert.AreEqual(2, coords.Count);
            int chebyshev = Math.Max(Math.Abs(coords[0].X - coords[1].X), Math.Abs(coords[0].Y - coords[1].Y));
            Assert.GreaterOrEqual(chebyshev, 3,
                $"Los 2 refuerzos quedaron pegados: {coords[0]} y {coords[1]} (distancia Chebyshev {chebyshev}).");
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

        // --- Respawn loop -----------------------------------------------------------

        [Test]
        public void Tick_FirstTickBelowGate_SpawnsExactlyCount()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnReinforcements { EnemyToSpawn = _enemyToSpawn, Count = 2 };

            var result = node.Tick(NewContext(Guid.NewGuid()));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_WaveStillAlive_SpawnsNothingOnSubsequentTicks()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var boss = Guid.NewGuid();
            var node = new AINode_SpawnReinforcements
            {
                EnemyToSpawn = _enemyToSpawn, Count = 2, RespawnDelayTurns = 2,
            };

            node.Tick(NewContext(boss)); // Oleada 1.
            Assert.AreEqual(2, _turnOrder.ParticipantCount);

            // Con la oleada aún viva, más ticks del boss no spawnean nada.
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext(boss)));
                Assert.AreEqual(2, _turnOrder.ParticipantCount,
                    "No debe spawnear mientras haya un refuerzo vivo.");
            }
        }

        [Test]
        public void Tick_AfterWaveDies_WaitsRespawnDelayTurns_ThenSpawnsExactlyCount()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var boss = Guid.NewGuid();
            var node = new AINode_SpawnReinforcements
            {
                EnemyToSpawn = _enemyToSpawn, Count = 2, RespawnDelayTurns = 2,
            };

            node.Tick(NewContext(boss)); // Oleada 1.
            Assert.AreEqual(2, _turnOrder.ParticipantCount);

            WipeCurrentWave(); // Player aniquila la oleada.
            Assert.AreEqual(0, _turnOrder.ParticipantCount);

            // Espera exactamente RespawnDelayTurns (2) turnos del boss sin spawnear.
            node.Tick(NewContext(boss));
            Assert.AreEqual(0, _turnOrder.ParticipantCount, "Turno de espera 1: no respawnea todavía.");
            node.Tick(NewContext(boss));
            Assert.AreEqual(0, _turnOrder.ParticipantCount, "Turno de espera 2: no respawnea todavía.");

            // Cumplido el delay, la siguiente ejecución spawnea otra oleada de Count.
            var result = node.Tick(NewContext(boss));
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, _turnOrder.ParticipantCount, "Oleada 2 spawnea exactamente Count.");
        }

        [Test]
        public void Tick_AfterWaveDies_WithZeroDelay_RespawnsNextTurn()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var boss = Guid.NewGuid();
            var node = new AINode_SpawnReinforcements
            {
                EnemyToSpawn = _enemyToSpawn, Count = 2, RespawnDelayTurns = 0,
            };

            node.Tick(NewContext(boss)); // Oleada 1.
            WipeCurrentWave();
            Assert.AreEqual(0, _turnOrder.ParticipantCount);

            node.Tick(NewContext(boss)); // Delay 0 ⇒ respawnea de inmediato el próximo turno.
            Assert.AreEqual(2, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_RuntimeStateResetsForFreshCombat()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var boss = Guid.NewGuid();
            var node = new AINode_SpawnReinforcements
            {
                EnemyToSpawn = _enemyToSpawn, Count = 2, RespawnDelayTurns = 2,
            };

            node.Tick(NewContext(boss)); // Ensucia el estado runtime: oleada viva, _hasSpawnedOnce.
            Assert.AreEqual(2, _turnOrder.ParticipantCount);

            // Combate nuevo = copia deep del árbol (mismo path que EnemyDataSO.CreateRuntimeAIRoot).
            var fresh = SerializationUtility.CreateCopy(node) as AINode_SpawnReinforcements;
            Assert.IsNotNull(fresh);

            // Servicios de combate nuevos (otra pelea).
            _turnOrder = new TurnOrderService();
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);

            // La copia arranca limpia: su primer tick spawnea la oleada inicial (no cree que
            // ya haya una oleada viva del combate anterior).
            fresh.Tick(NewContext(boss));
            Assert.AreEqual(2, _turnOrder.ParticipantCount,
                "El clon runtime no debe heredar la oleada del combate previo.");
        }

        /// <summary>
        /// Aniquila la oleada viva espejando el entierro de <c>CombatDeathWatcher</c>:
        /// Health a 0 (fuente de verdad del alive-check) y salida del turn order.
        /// </summary>
        private void WipeCurrentWave()
        {
            var snapshot = new List<Guid>(_turnOrder.OrderForRound);
            foreach (var guid in snapshot)
            {
                _attributes.SetAttributeValue<Health, int>(guid, 0);
                _turnOrder.Remove(guid);
            }
        }
    }
}
