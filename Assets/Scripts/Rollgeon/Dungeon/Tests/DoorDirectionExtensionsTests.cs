using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using UnityEngine;

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

        // FromLocalPosition: dominant axis wins, ties go to the Z axis (North/South).
        [Test] public void FromLocalPosition_PositiveZ_IsNorth() => Assert.AreEqual(DoorDirection.North, DoorDirectionExtensions.FromLocalPosition(new Vector3(0f, 0f,  3f)));
        [Test] public void FromLocalPosition_NegativeZ_IsSouth() => Assert.AreEqual(DoorDirection.South, DoorDirectionExtensions.FromLocalPosition(new Vector3(0f, 0f, -3f)));
        [Test] public void FromLocalPosition_PositiveX_IsEast()  => Assert.AreEqual(DoorDirection.East,  DoorDirectionExtensions.FromLocalPosition(new Vector3( 3f, 0f, 0f)));
        [Test] public void FromLocalPosition_NegativeX_IsWest()  => Assert.AreEqual(DoorDirection.West,  DoorDirectionExtensions.FromLocalPosition(new Vector3(-3f, 0f, 0f)));
        [Test] public void FromLocalPosition_TieBreaksToZAxis()  => Assert.AreEqual(DoorDirection.North, DoorDirectionExtensions.FromLocalPosition(new Vector3(2f, 0f, 2f)));
        [Test] public void FromLocalPosition_IgnoresY()          => Assert.AreEqual(DoorDirection.East,  DoorDirectionExtensions.FromLocalPosition(new Vector3(3f, 100f, 0f)));
    }
}
