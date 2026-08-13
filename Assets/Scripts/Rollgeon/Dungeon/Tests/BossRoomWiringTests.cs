using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Entities.Bosses;
using UnityEditor;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión del wiring de bosses por piso. El bug original tenía las tres salas de boss
    /// apuntando al mismo pool (solo Boss 1), así que el Boss 2 y el Boss 3 nunca spawneaban
    /// ("el Boss 2 no hace nada"). Estos tests cargan los <see cref="FloorLayoutSO"/> reales y
    /// verifican que cada piso puede spawnear su boss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El wiring vive en dos lugares desde que existe el <see cref="BossPoolSO"/> por piso:
    /// si el layout tiene <see cref="FloorLayoutSO.BossPool"/> manda el pool; si es
    /// <c>null</c> sigue mandando el <c>EnemyPool</c> de las salas del slot Boss
    /// (comportamiento previo). Los tests aceptan las dos formas para no romper mientras
    /// integración autorea los assets de pool.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class BossRoomWiringTests
    {
        private const string Floor1 = "Assets/Rollgeon/Floor/FloorLayout.asset";
        private const string Floor2 = "Assets/Rollgeon/Floor/Floor2_Layout.asset";
        private const string Floor3 = "Assets/Rollgeon/Floor/Floor3_Layout.asset";

        private static readonly string[] AllFloors = { Floor1, Floor2, Floor3 };

        // legacyBossId: el boss del wiring previo (manda si el layout no tiene pool). Con pool,
        // los pisos 2 y 3 lo SUPLANTAN por diseño (12/08): el viejo queda en el pool desactivado
        // (re-activable desde el Inspector), y los activos son los jefes nuevos.
        [TestCase(Floor1, "boss.sunken_grand", new[] { "boss.sunken_grand", "boss.croupier", "boss.one_armed" })]
        [TestCase(Floor2, "boss.security_boss", new[] { "boss.cashier", "boss.scorekeeper" })]
        [TestCase(Floor3, "boss.general_director", new[] { "boss.la_generala", "boss.tahur" })]
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

            // El boss viejo suplantado no desaparece: queda en el pool, desactivado.
            var allPoolIds = layout.BossPool.Entries
                .Where(e => e?.Boss != null)
                .Select(e => e.Boss.EntityId)
                .ToList();
            CollectionAssert.Contains(allPoolIds, legacyBossId,
                $"{layout.name}: '{legacyBossId}' debería seguir en el pool (desactivado), " +
                "para poder re-activarlo desde el Inspector.");
        }

        [Test]
        public void FloorPools_DoNotShareActiveBosses()
        {
            // Arrange — este assert solo tiene sentido sobre los pools nuevos: los assets
            // los crea integración, así que sin ellos el test se salta en vez de fallar.
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

            // Assert — un boss compartido entre pisos es el bug original volviendo.
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
            // La invariante de diseño: un piso sin boss activo no es jugable.
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
        /// Bosses que el piso puede spawnear hoy: el pool nuevo si está asignado, si no el
        /// <c>EnemyPool</c> de las salas del slot Boss (wiring previo).
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
