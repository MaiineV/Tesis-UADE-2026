using NUnit.Framework;
using Rollgeon.Dungeon.Components;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class DoorDirectionExtensionsTests
    {
        [Test] public void North_Opposite_IsSouth() => Assert.AreEqual(DoorDirection.South, DoorDirection.North.Opposite());
        [Test] public void South_Opposite_IsNorth() => Assert.AreEqual(DoorDirection.North, DoorDirection.South.Opposite());
        [Test] public void East_Opposite_IsWest()   => Assert.AreEqual(DoorDirection.West, DoorDirection.East.Opposite());
        [Test] public void West_Opposite_IsEast()   => Assert.AreEqual(DoorDirection.East, DoorDirection.West.Opposite());

        [Test] public void North_DoorStateKey() => Assert.AreEqual("door_N", DoorDirection.North.DoorStateKey());
        [Test] public void South_DoorStateKey() => Assert.AreEqual("door_S", DoorDirection.South.DoorStateKey());
        [Test] public void East_DoorStateKey()  => Assert.AreEqual("door_E", DoorDirection.East.DoorStateKey());
        [Test] public void West_DoorStateKey()  => Assert.AreEqual("door_W", DoorDirection.West.DoorStateKey());
    }
}
