using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Grid;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Round-trip de <see cref="DungeonSnapshot"/> por el camino REAL del save
    /// (Odin <see cref="SerializationUtility"/> JSON, no Unity JsonUtility) —
    /// gatekeeper de la persistencia de dungeon (Feature#0028). Verifica que la
    /// identidad espacial y los <see cref="RoomObjectState"/> polimórficos anidados
    /// sobrevivan con su subtipo concreto.
    /// </summary>
    [TestFixture]
    public class DungeonSnapshotSerializationTests
    {
        private static DungeonSnapshot RoundTrip(DungeonSnapshot src)
        {
            byte[] bytes = SerializationUtility.SerializeValue(src, DataFormat.JSON);
            return SerializationUtility.DeserializeValue<DungeonSnapshot>(bytes, DataFormat.JSON);
        }

        [Test]
        public void RoundTrip_EmptySnapshot_PreservesIdentity()
        {
            var src = new DungeonSnapshot
            {
                CurrentCell = new Vector2Int(2, -1),
                LastEntryDirection = DoorDirection.West,
                PlayerCoord = new GridCoord(4, 3),
                PlayerGuid = "abc-123",
            };

            var hydrated = RoundTrip(src);

            Assert.AreEqual(new Vector2Int(2, -1), hydrated.CurrentCell);
            Assert.AreEqual(DoorDirection.West, hydrated.LastEntryDirection);
            Assert.AreEqual(new GridCoord(4, 3), hydrated.PlayerCoord);
            Assert.AreEqual("abc-123", hydrated.PlayerGuid);
            Assert.IsNotNull(hydrated.Rooms);
            Assert.AreEqual(0, hydrated.Rooms.Count);
        }

        [Test]
        public void RoundTrip_NullLastEntryDirection_StaysNull()
        {
            var src = new DungeonSnapshot { LastEntryDirection = null };

            var hydrated = RoundTrip(src);

            Assert.IsFalse(hydrated.LastEntryDirection.HasValue);
        }

        [Test]
        public void RoundTrip_RoomWithPolymorphicObjectStates_PreservesSubtypes()
        {
            var room = new RoomSnapshot
            {
                Cell = new Vector2Int(1, 0),
                State = RoomState.Uncleared,
                Visited = true,
            };
            room.ObjectStates["enemy_0"] = new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "goblin_01",
                CurrentHP = 7,
                IsDead = false,
                SpawnPointIndex = 1,
                Tier = 2,
            };
            room.ObjectStates["door_N"] = new DoorState
            {
                SpawnPointId = "door_N",
                Direction = DoorDirection.North,
                Forced = true,
                Unlocked = false,
            };

            var src = new DungeonSnapshot();
            src.Rooms.Add(room);

            var hydrated = RoundTrip(src);

            Assert.AreEqual(1, hydrated.Rooms.Count);
            var r = hydrated.Rooms[0];
            Assert.AreEqual(new Vector2Int(1, 0), r.Cell);
            Assert.AreEqual(RoomState.Uncleared, r.State);
            Assert.IsTrue(r.Visited);

            Assert.IsInstanceOf<EnemySpawnState>(r.ObjectStates["enemy_0"]);
            var enemy = (EnemySpawnState)r.ObjectStates["enemy_0"];
            Assert.AreEqual("goblin_01", enemy.EnemyDataSOId);
            Assert.AreEqual(7, enemy.CurrentHP);
            Assert.AreEqual(2, enemy.Tier);
            Assert.IsFalse(enemy.IsDead);

            Assert.IsInstanceOf<DoorState>(r.ObjectStates["door_N"]);
            var door = (DoorState)r.ObjectStates["door_N"];
            Assert.AreEqual(DoorDirection.North, door.Direction);
            Assert.IsTrue(door.Forced);
        }
    }
}
