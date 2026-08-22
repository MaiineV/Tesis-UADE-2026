using NUnit.Framework;
using Rollgeon.Dungeon;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Matriz completa del mapeo estado→sprite del minimapa (spec fija del usuario:
    /// slices Minimap_0..8). Cualquier cambio acá es un cambio de spec, no un refactor.
    /// </summary>
    [TestFixture]
    public class MinimapSpriteMapTests
    {
        // Salas normales: terna 0/1/2.
        [TestCase(RoomType.Combat, true, true, 1)]
        [TestCase(RoomType.Combat, false, true, 2)]
        [TestCase(RoomType.Combat, false, false, 0)]
        [TestCase(RoomType.Start, true, true, 1)]
        [TestCase(RoomType.Start, false, true, 2)]
        [TestCase(RoomType.Start, false, false, 0)]
        [TestCase(RoomType.Potion, true, true, 1)]
        [TestCase(RoomType.Potion, false, true, 2)]
        [TestCase(RoomType.Potion, false, false, 0)]
        // Tienda: 3 actual, 6 no-actual (visitada o adyacente sin visitar).
        [TestCase(RoomType.Shop, true, true, 3)]
        [TestCase(RoomType.Shop, false, true, 6)]
        [TestCase(RoomType.Shop, false, false, 6)]
        // Encantamientos: 4 actual, 5 no-actual.
        [TestCase(RoomType.Enchantment, true, true, 4)]
        [TestCase(RoomType.Enchantment, false, true, 5)]
        [TestCase(RoomType.Enchantment, false, false, 5)]
        // Boss: 8 actual, 7 no-actual (visitado o sin visitar).
        [TestCase(RoomType.Boss, true, true, 8)]
        [TestCase(RoomType.Boss, false, true, 7)]
        [TestCase(RoomType.Boss, false, false, 7)]
        public void Resolve_ReturnsSpecSpriteIndex(
            RoomType type, bool isCurrent, bool isVisited, int expected)
        {
            Assert.AreEqual(expected, MinimapSpriteMap.Resolve(type, isCurrent, isVisited));
        }

        [Test]
        public void Resolve_CurrentWinsOverVisited_ForSpecialRooms()
        {
            // La sala actual siempre está visitada — el orden de chequeo (IsCurrent
            // primero) es lo que evita que la actual caiga al sprite "no-actual".
            Assert.AreEqual(8, MinimapSpriteMap.Resolve(RoomType.Boss, isCurrent: true, isVisited: true));
            Assert.AreEqual(3, MinimapSpriteMap.Resolve(RoomType.Shop, isCurrent: true, isVisited: true));
            Assert.AreEqual(4, MinimapSpriteMap.Resolve(RoomType.Enchantment, isCurrent: true, isVisited: true));
        }
    }
}
