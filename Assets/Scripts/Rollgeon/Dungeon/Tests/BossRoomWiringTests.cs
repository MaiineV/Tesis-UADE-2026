using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Entities.Bosses;
using UnityEditor;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión del wiring de bosses por piso: carga los <see cref="FloorLayoutSO"/> reales y
    /// verifica que cada piso pueda spawnear su boss.
    /// </summary>
    /// <remarks>
    /// El wiring vive en dos lugares: si el layout tiene <see cref="FloorLayoutSO.BossPool"/> manda
    /// el pool; si es <c>null</c> manda el <c>EnemyPool</c> de las salas del slot Boss. Los tests
    /// aceptan las dos formas mientras integración autorea los assets de pool.
    /// </remarks>
    [TestFixture]
    public class BossRoomWiringTests
    {
        private const string Floor1 = "Assets/Rollgeon/Floor/FloorLayout.asset";
        private const string Floor2 = "Assets/Rollgeon/Floor/Floor2_Layout.asset";
        private const string Floor3 = "Assets/Rollgeon/Floor/Floor3_Layout.asset";

        private static readonly string[] AllFloors = { Floor1, Floor2, Floor3 };

        // legacyBossId: el boss del wiring previo, que manda si el layout no tiene pool. Con pool,
        // queda en él desactivado y el activo es el jefe nuevo — uno solo por piso, el terminado.
        [TestCase(Floor1, "boss.sunken_grand", new[] { "boss.croupier" })]
        [TestCase(Floor2, "boss.security_boss", new[] { "boss.cashier" })]
        [TestCase(Floor3, "boss.general_director", new[] { "boss.la_generala" })]
        public void FloorBossRoom_ResolvesToExpectedBoss(
            string layoutPath, string legacyBossId, string[] expectedActiveWithPool)
        {
            // Arrange
            var layout = LoadLayout(layoutPath);

            // Act
            var bossEntityIds = SpawnableBossIdsFor(layout, out var source);

            // Assert
            if (layout.BossPool == null)
            {
                CollectionAssert.Contains(bossEntityIds, legacyBossId,
                    $"{layout.name} ({source}): la sala de boss debería poder spawnear " +
                    $"'{legacyBossId}' pero resuelve a [{string.Join(", ", bossEntityIds)}].");
                return;
            }

            CollectionAssert.AreEquivalent(expectedActiveWithPool, bossEntityIds,
                $"{layout.name} ({source}): los bosses ACTIVOS del piso no son los del diseño. " +
                $"Resuelve a [{string.Join(", ", bossEntityIds)}].");

            // El viejo no desaparece: queda en el pool, desactivado.
            var allPoolIds = layout.BossPool.Entries
                .Where(e => e?.Boss != null)
                .Select(e => e.Boss.EntityId)
                .ToList();
            CollectionAssert.Contains(allPoolIds, legacyBossId,
                $"{layout.name}: '{legacyBossId}' debería seguir en el pool (desactivado).");
            CollectionAssert.DoesNotContain(bossEntityIds, legacyBossId,
                $"{layout.name}: '{legacyBossId}' no tiene rig — un jefe congelado no puede salir.");
        }

        [TestCase(Floor1, "boss.croupier")]
        [TestCase(Floor2, "boss.cashier")]
        [TestCase(Floor3, "boss.la_generala")]
        public void FloorPool_GivesTheMainBossEveryRun(string layoutPath, string mainBossId)
        {
            // Arrange
            var pool = LoadLayout(layoutPath).BossPool;
            Assert.IsNotNull(pool, $"{layoutPath} no tiene BossPool asignado.");

            // Act
            float total = 0f;
            float main = 0f;
            foreach (var entry in pool.Entries)
            {
                if (!BossPoolSO.IsActive(entry)) continue;
                total += entry.Weight;
                if (entry.Boss.EntityId == mainBossId) main = entry.Weight;
            }

            // Assert
            Assert.Greater(total, 0f, $"{pool.name}: ningún boss activo.");
            Assert.AreEqual(1f, main / total, 0.001f,
                $"{pool.name}: '{mainBossId}' debería llevarse el 100% del roll. " +
                $"Pesos activos: [{string.Join(", ", pool.Entries.Where(BossPoolSO.IsActive).Select(e => $"{e.Boss.EntityId}={e.Weight}"))}].");
        }

        /// <summary>Re-activar un jefe en banco tiene que ser poner <c>Enabled = on</c> y nada más.</summary>
        [TestCase(Floor1, "boss.one_armed")]
        [TestCase(Floor2, "boss.scorekeeper")]
        [TestCase(Floor3, "boss.tahur")]
        public void TheBenchedBosses_StayInThePoolWithTheirRoom(string layoutPath, string benchedId)
        {
            // Arrange
            var pool = LoadLayout(layoutPath).BossPool;
            Assert.IsNotNull(pool, $"{layoutPath} no tiene BossPool asignado.");

            // Act
            var entry = pool.Entries.FirstOrDefault(e => e?.Boss != null && e.Boss.EntityId == benchedId);

            // Assert
            Assert.IsNotNull(entry, $"{pool.name}: '{benchedId}' desapareció del pool en vez de " +
                                    "quedar desactivado.");
            Assert.IsFalse(BossPoolSO.IsActive(entry),
                $"{pool.name}: '{benchedId}' está activo. Sólo salen los tres terminados.");
            Assert.IsNotNull(entry.Room,
                $"{pool.name}: '{benchedId}' perdió su sala mientras estaba en banco — " +
                "re-activarlo lo dejaría peleando en una sala cualquiera del piso.");
        }

        [Test]
        public void FloorPools_DoNotShareActiveBosses()
        {
            // Arrange — sin los pools nuevos el test se salta en vez de fallar.
            var pools = new List<(string floor, BossPoolSO pool)>();
            foreach (var path in AllFloors)
            {
                var pool = LoadLayout(path).BossPool;
                if (pool == null)
                {
                    Assert.Ignore(
                        $"{path} no tiene BossPool asignado todavía — los assets de pool los " +
                        "crea integración. El wiring previo lo cubre " +
                        $"{nameof(FloorBossRoom_ResolvesToExpectedBoss)}.");
                }
                pools.Add((path, pool));
            }

            // Act
            var activeIdsPerFloor = pools
                .Select(p => (p.floor, ids: ActiveBossIds(p.pool)))
                .ToList();
            var allActiveIds = activeIdsPerFloor.SelectMany(p => p.ids).ToList();

            // Assert
            var shared = allActiveIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.IsEmpty(shared,
                "Ningún boss ACTIVO debería repetirse entre pools de pisos distintos. " +
                $"Compartidos: [{string.Join(", ", shared)}]. Por piso: " +
                string.Join(" | ", activeIdsPerFloor.Select(p => $"{p.floor}: [{string.Join(", ", p.ids)}]")));
        }

        [Test]
        public void EachFloorPool_HasAtLeastOneActiveBoss()
        {
            foreach (var path in AllFloors)
            {
                var layout = LoadLayout(path);
                var pool = layout.BossPool;
                if (pool == null)
                {
                    Assert.Ignore(
                        $"{path} no tiene BossPool asignado todavía — los assets de pool los " +
                        "crea integración.");
                }

                CollectionAssert.IsNotEmpty(ActiveBossIds(pool),
                    $"{layout.name}: el BossPool '{pool.name}' no tiene ningún boss activo " +
                    "(Weight > 0 y Enabled = on).");
            }
        }

        /// <summary>
        /// Los únicos con terreno autorado propio. Los viejos pelean en la sala compartida del
        /// piso: su entry va sin <c>Room</c> a propósito.
        /// </summary>
        private static readonly HashSet<string> BossesWithOwnRoom = new HashSet<string>
        {
            "boss.croupier", "boss.one_armed",
            "boss.cashier", "boss.scorekeeper",
            "boss.la_generala", "boss.tahur",
        };

        [TestCase(Floor1)]
        [TestCase(Floor2)]
        [TestCase(Floor3)]
        public void EveryNewBoss_HasItsOwnRoomWired(string layoutPath)
        {
            // Arrange — el cableado lo escribe 'Tools/Rollgeon/Bosses/Build Floor Pools' y las
            // salas 'Rollgeon/Bosses/Build Boss Rooms'; olvidar uno de los dos no falla en runtime.
            var layout = LoadLayout(layoutPath);
            Assert.IsNotNull(layout.BossPool, $"'{layout.name}' no tiene BossPool asignado.");

            // Act / Assert — TODAS las entries, no sólo las activas: los jefes en banco tienen que
            // conservar su cableado, o "re-activar" deja de ser un toggle.
            foreach (var entry in layout.BossPool.Entries)
            {
                if (entry?.Boss == null) continue;

                // Sin Room = "sorteá una sala del piso", el camino legacy de los jefes viejos.
                if (!BossesWithOwnRoom.Contains(entry.Boss.EntityId))
                {
                    Assert.IsNull(entry.Room,
                        $"'{entry.Boss.EntityId}' es un jefe viejo y no debería tener Room propia: " +
                        "pelea en la sala compartida del piso.");
                    continue;
                }

                Assert.IsNotNull(entry.Room,
                    $"'{entry.Boss.EntityId}' está en '{layout.BossPool.name}' sin Room: el piso le " +
                    "va a sortear una sala cualquiera, con los obstáculos de otro jefe. Correr " +
                    "'Rollgeon → Bosses → Build Boss Rooms' y después " +
                    "'Tools → Rollgeon → Bosses → Build Floor Pools'.");
                Assert.AreEqual(RoomType.Boss, entry.Room.Type,
                    $"La sala de '{entry.Boss.EntityId}' no es de tipo Boss.");
                Assert.IsNotNull(entry.Room.RoomPrefab,
                    $"La sala de '{entry.Boss.EntityId}' no tiene RoomPrefab.");
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static FloorLayoutSO LoadLayout(string layoutPath)
        {
            var layout = AssetDatabase.LoadAssetAtPath<FloorLayoutSO>(layoutPath);
            Assert.IsNotNull(layout, $"No se encontró el FloorLayout en {layoutPath}");
            return layout;
        }

        private static List<string> ActiveBossIds(BossPoolSO pool)
            => pool.ActiveBosses()
                .Where(boss => boss != null)
                .Select(boss => boss.EntityId)
                .ToList();

        /// <summary>
        /// El pool nuevo si está asignado; si no, el <c>EnemyPool</c> de las salas del slot Boss.
        /// </summary>
        private static List<string> SpawnableBossIdsFor(FloorLayoutSO layout, out string source)
        {
            if (layout.BossPool != null)
            {
                source = $"BossPool '{layout.BossPool.name}'";
                return ActiveBossIds(layout.BossPool);
            }

            source = "EnemyPool de las salas (wiring previo)";
            var bossSlot = layout.Slots.SingleOrDefault(s => s.Type == RoomType.Boss);
            Assert.IsNotNull(bossSlot, $"{layout.name} no tiene un slot de tipo Boss.");
            CollectionAssert.IsNotEmpty(bossSlot.Pool, $"{layout.name}: el slot Boss no tiene salas en el Pool.");

            return bossSlot.Pool
                .Where(room => room != null && room.EnemyPool != null)
                .SelectMany(room => room.EnemyPool.Entries)
                .Where(entry => entry.Item != null)
                .Select(entry => entry.Item.EntityId)
                .ToList();
        }
    }
}
