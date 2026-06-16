using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión del wiring de bosses por piso. El bug original tenía las tres salas de boss
    /// apuntando al mismo pool (solo Boss 1), así que el Boss 2 y el Boss 3 nunca spawneaban
    /// ("el Boss 2 no hace nada"). Estos tests cargan los <see cref="FloorLayoutSO"/> reales y
    /// verifican que cada piso resuelve a su boss correspondiente.
    /// </summary>
    [TestFixture]
    public class BossRoomWiringTests
    {
        private const string Floor1 = "Assets/Rollgeon/Floor/FloorLayout.asset";
        private const string Floor2 = "Assets/Rollgeon/Floor/Floor2_Layout.asset";
        private const string Floor3 = "Assets/Rollgeon/Floor/Floor3_Layout.asset";

        [TestCase(Floor1, "boss.sunken_grand")]
        [TestCase(Floor2, "boss.security_boss")]
        [TestCase(Floor3, "boss.general_director")]
        public void FloorBossRoom_ResolvesToExpectedBoss(string layoutPath, string expectedEntityId)
        {
            // Arrange
            var bossEntityIds = BossEntityIdsFor(layoutPath, out var layoutName);

            // Assert
            CollectionAssert.Contains(bossEntityIds, expectedEntityId,
                $"{layoutName}: la sala de boss debería poder spawnear '{expectedEntityId}' " +
                $"pero su pool resuelve a [{string.Join(", ", bossEntityIds)}].");
        }

        [Test]
        public void EachFloor_SpawnsADistinctBoss()
        {
            // Arrange / Act
            var allBosses = new[] { Floor1, Floor2, Floor3 }
                .SelectMany(p => BossEntityIdsFor(p, out _))
                .ToList();

            // Assert
            Assert.AreEqual(allBosses.Count, allBosses.Distinct().Count(),
                $"Ningún boss debería repetirse entre pisos (bug original: el mismo boss en los tres). " +
                $"Resuelto: [{string.Join(", ", allBosses)}].");
        }

        private static System.Collections.Generic.List<string> BossEntityIdsFor(
            string layoutPath, out string layoutName)
        {
            var layout = AssetDatabase.LoadAssetAtPath<FloorLayoutSO>(layoutPath);
            Assert.IsNotNull(layout, $"No se encontró el FloorLayout en {layoutPath}");
            layoutName = layout.name;

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
