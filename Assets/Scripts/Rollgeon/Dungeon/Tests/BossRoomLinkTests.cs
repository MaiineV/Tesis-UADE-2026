using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// El vínculo jefe → sala: el pool del piso rolea el jefe durante la generación y su
    /// <see cref="WeightedBoss.Room"/> pisa la sala que había elegido el pool de salas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lo que se fija acá es el modelo: <b>el jefe manda y la sala viene con él</b>. Al revés
    /// —la sala nombrando a su jefe— dos jefes no podrían compartir sala sin duplicar el asset.
    /// </para>
    /// <para>
    /// Y se fija que el roleo <b>no consuma el rng del planner</b>. Usa su propio
    /// <see cref="BossSeed"/>, así que los pisos de un seed que ya existía siguen saliendo
    /// iguales: si alguien lo pasa al rng compartido, el último test se pone rojo.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class BossRoomLinkTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // =====================================================================
        // El vínculo
        // =====================================================================

        [Test]
        public void AssignBosses_EntryWithRoom_OverridesTheRoomThePoolHadPicked()
        {
            // Arrange
            var ownRoom = CreateRoom("boss_croupier", RoomType.Boss);
            var layout = CreateLayout(CreatePool((CreateBoss("boss.croupier"), ownRoom, 1f, true)));
            var cell = new Vector2Int(3, 0);
            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [cell] = CreateRoom("boss_generica", RoomType.Boss),
            };

            // Act
            var bossByCell = FloorTopologyPlanner.AssignBosses(layout, 1234, assignments, new List<string>());

            // Assert
            Assert.AreSame(ownRoom, assignments[cell],
                "La sala del boss rolado tiene que pisar la que había elegido el pool de salas.");
            Assert.AreEqual("boss.croupier", bossByCell[cell].EntityId);
        }

        [Test]
        public void AssignBosses_EntryWithoutRoom_LeavesTheRoomThePoolHadPicked()
        {
            // Arrange — los jefes viejos no tienen sala propia y no deben romperse.
            var pooled = CreateRoom("boss_generica", RoomType.Boss);
            var layout = CreateLayout(CreatePool((CreateBoss("boss.sunken_grand"), null, 1f, true)));
            var cell = new Vector2Int(3, 0);
            var assignments = new Dictionary<Vector2Int, RoomSO> { [cell] = pooled };

            // Act
            var bossByCell = FloorTopologyPlanner.AssignBosses(layout, 1234, assignments, new List<string>());

            // Assert
            Assert.AreSame(pooled, assignments[cell],
                "Sin Room en la entry, la sala sigue saliendo del pool como antes del vínculo.");
            Assert.AreEqual("boss.sunken_grand", bossByCell[cell].EntityId);
        }

        [Test]
        public void AssignBosses_TwoBossesSharingOneRoom_BothResolveToIt()
        {
            // Arrange — el caso que descartó el modelo inverso: una sala, dos jefes, sin duplicar
            // el asset. Cuál sale depende del seed; lo que se fija es que la sala sea la misma.
            var shared = CreateRoom("boss_compartida", RoomType.Boss);
            var layout = CreateLayout(CreatePool(
                (CreateBoss("boss.a"), shared, 1f, true),
                (CreateBoss("boss.b"), shared, 1f, true)));
            var cell = new Vector2Int(3, 0);

            var salidas = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                var assignments = new Dictionary<Vector2Int, RoomSO>
                {
                    [cell] = CreateRoom($"boss_generica_{seed}", RoomType.Boss),
                };

                // Act
                var bossByCell = FloorTopologyPlanner.AssignBosses(
                    layout, seed, assignments, new List<string>());

                // Assert
                Assert.AreSame(shared, assignments[cell],
                    $"Seed {seed}: los dos jefes comparten sala, así que siempre sale la misma.");
                salidas.Add(bossByCell[cell].EntityId);
            }

            CollectionAssert.AreEquivalent(new[] { "boss.a", "boss.b" }, salidas,
                "Con pesos iguales en 40 seeds tienen que haber salido los dos.");
        }

        [Test]
        public void AssignBosses_NonBossCells_AreLeftAlone()
        {
            // Arrange
            var layout = CreateLayout(CreatePool((CreateBoss("boss.a"), CreateRoom("r", RoomType.Boss), 1f, true)));
            var combat = CreateRoom("combate", RoomType.Combat);
            var combatCell = new Vector2Int(1, 0);
            var assignments = new Dictionary<Vector2Int, RoomSO> { [combatCell] = combat };

            // Act
            var bossByCell = FloorTopologyPlanner.AssignBosses(layout, 7, assignments, new List<string>());

            // Assert
            Assert.AreSame(combat, assignments[combatCell]);
            CollectionAssert.IsEmpty(bossByCell);
        }

        [Test]
        public void AssignBosses_WithoutBossPool_ChangesNothing()
        {
            // Arrange — un piso sin pool no debe reventar ni tocar la sala.
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            _created.Add(layout);
            var pooled = CreateRoom("boss_generica", RoomType.Boss);
            var cell = new Vector2Int(3, 0);
            var assignments = new Dictionary<Vector2Int, RoomSO> { [cell] = pooled };

            // Act
            var bossByCell = FloorTopologyPlanner.AssignBosses(layout, 99, assignments, new List<string>());

            // Assert
            Assert.AreSame(pooled, assignments[cell]);
            CollectionAssert.IsEmpty(bossByCell);
        }

        // =====================================================================
        // Determinismo
        // =====================================================================

        [Test]
        public void AssignBosses_SameSeed_GivesTheSameBossAndRoom()
        {
            // Arrange — el piso se reconstruye del seed en cada carga y el resume no persiste
            // topología: si esto no fuera estable, recargar te cambiaría el jefe.
            var roomA = CreateRoom("sala_a", RoomType.Boss);
            var roomB = CreateRoom("sala_b", RoomType.Boss);
            var layout = CreateLayout(CreatePool(
                (CreateBoss("boss.a"), roomA, 1f, true),
                (CreateBoss("boss.b"), roomB, 1f, true)));
            var cell = new Vector2Int(3, 0);

            // Act
            var first = RunOnce(layout, 4242, cell, out var firstRoom);
            var second = RunOnce(layout, 4242, cell, out var secondRoom);

            // Assert
            Assert.AreEqual(first, second, "Mismo seed tiene que dar el mismo jefe.");
            Assert.AreSame(firstRoom, secondRoom, "Mismo seed tiene que dar la misma sala.");
        }

        [Test]
        public void AssignBosses_DoesNotConsumeThePlannerRng()
        {
            // Arrange — dos layouts idénticos salvo el pool de bosses. Si el roleo del jefe
            // consumiera el rng compartido, el resto del piso saldría distinto entre los dos.
            var sinPool = CreateFullLayout(bossPool: null);
            var conPool = CreateFullLayout(bossPool: CreatePool(
                (CreateBoss("boss.a"), CreateRoom("sala_a", RoomType.Boss), 1f, true)));

            // Act
            var planSin = FloorTopologyPlanner.Generate(sinPool, 31337);
            var planCon = FloorTopologyPlanner.Generate(conPool, 31337);

            // Assert
            CollectionAssert.AreEquivalent(planSin.Cells, planCon.Cells,
                "El pool de bosses no puede cambiar la topología del piso.");

            var noBossSin = planSin.Assignments
                .Where(kv => kv.Value != null && kv.Value.Type != RoomType.Boss)
                .OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y)
                .Select(kv => $"{kv.Key}:{kv.Value.RoomId}").ToList();
            var noBossCon = planCon.Assignments
                .Where(kv => kv.Value != null && kv.Value.Type != RoomType.Boss)
                .OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y)
                .Select(kv => $"{kv.Key}:{kv.Value.RoomId}").ToList();

            CollectionAssert.AreEqual(noBossSin, noBossCon,
                "El roleo del jefe usa su propio BossSeed: no puede correr la secuencia del rng " +
                "del planner ni cambiar las salas que no son de boss.");
        }

        [Test]
        public void BossSeed_Derive_IsPureAndSeparatesCells()
        {
            Assert.AreEqual(BossSeed.Derive(7, new Vector2Int(2, 3)),
                            BossSeed.Derive(7, new Vector2Int(2, 3)));
            Assert.AreNotEqual(BossSeed.Derive(7, new Vector2Int(2, 3)),
                               BossSeed.Derive(7, new Vector2Int(3, 2)));
            Assert.AreNotEqual(BossSeed.Derive(7, new Vector2Int(2, 3)),
                               Chests.ChestSeed.Derive(7, new Vector2Int(2, 3)),
                               "El salt tiene que decorrelar el roll del boss del del cofre.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private string RunOnce(FloorLayoutSO layout, int seed, Vector2Int cell, out RoomSO room)
        {
            var assignments = new Dictionary<Vector2Int, RoomSO>
            {
                [cell] = CreateRoom("boss_generica", RoomType.Boss),
            };
            var bossByCell = FloorTopologyPlanner.AssignBosses(layout, seed, assignments, new List<string>());
            room = assignments[cell];
            return bossByCell[cell].EntityId;
        }

        private RoomSO CreateRoom(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.Type = type;
            room.GridSize = Vector2Int.one;
            _created.Add(room);
            return room;
        }

        private EnemyDataSO CreateBoss(string entityId)
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            boss.EntityId = entityId;
            _created.Add(boss);
            return boss;
        }

        private BossPoolSO CreatePool(params (EnemyDataSO boss, RoomSO room, float weight, bool enabled)[] entries)
        {
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.Entries = entries
                .Select(e => new WeightedBoss
                {
                    Boss = e.boss, Room = e.room, Weight = e.weight, Enabled = e.enabled,
                })
                .ToList();
            _created.Add(pool);
            return pool;
        }

        private FloorLayoutSO CreateLayout(BossPoolSO pool)
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.BossPool = pool;
            _created.Add(layout);
            return layout;
        }

        /// <summary>Layout con slots reales, para los tests que corren <c>Generate</c> entero.</summary>
        private FloorLayoutSO CreateFullLayout(BossPoolSO bossPool)
        {
            var layout = CreateLayout(bossPool);
            layout.Slots = new List<RoomTypeSlot>
            {
                new RoomTypeSlot
                {
                    Type = RoomType.Start,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                    Pool = new List<RoomSO> { CreateRoom("start", RoomType.Start) },
                },
                new RoomTypeSlot
                {
                    Type = RoomType.Combat,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 4 },
                    Pool = Enumerable.Range(0, 4)
                        .Select(i => CreateRoom($"combate_{i}", RoomType.Combat)).ToList(),
                },
                new RoomTypeSlot
                {
                    Type = RoomType.Shop,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                    Pool = new List<RoomSO> { CreateRoom("shop", RoomType.Shop) },
                },
                new RoomTypeSlot
                {
                    Type = RoomType.Boss,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                    Pool = new List<RoomSO> { CreateRoom("boss_generica", RoomType.Boss) },
                },
            };
            return layout;
        }
    }
}
