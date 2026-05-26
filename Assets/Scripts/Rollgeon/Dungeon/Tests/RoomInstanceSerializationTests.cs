using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Round-trip de <see cref="SerializableObjectStates"/> preservando el
    /// subtipo concreto de cada <see cref="RoomObjectState"/>. Gatekeeper de
    /// la persistencia in-memory y del futuro save a disco (§13.6.1).
    /// </summary>
    [TestFixture]
    public class RoomInstanceSerializationTests
    {
        [System.Serializable]
        private class Wrapper
        {
            public SerializableObjectStates States;
        }

        private static SerializableObjectStates RoundTrip(SerializableObjectStates src)
        {
            string json = JsonUtility.ToJson(new Wrapper { States = src });
            return JsonUtility.FromJson<Wrapper>(json).States;
        }

        [Test]
        public void RoundTrip_PreservesDoorStateSubtype()
        {
            var src = new SerializableObjectStates();
            src.Set("door_N", new DoorState
            {
                SpawnPointId = "door_N",
                Direction = DoorDirection.North,
                Forced = true,
                Unlocked = false
            });

            var hydrated = RoundTrip(src);

            Assert.IsTrue(hydrated.TryGet<DoorState>("door_N", out var door));
            Assert.AreEqual(DoorDirection.North, door.Direction);
            Assert.IsTrue(door.Forced);
            Assert.IsFalse(door.Unlocked);
            Assert.AreEqual("door_N", door.SpawnPointId);
        }

        [Test]
        public void RoundTrip_PreservesEnemySpawnStateSubtype()
        {
            var src = new SerializableObjectStates();
            src.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "goblin_01",
                CurrentHP = 13,
                IsDead = false,
                SpawnPointIndex = 2
            });

            var hydrated = RoundTrip(src);

            Assert.IsTrue(hydrated.TryGet<EnemySpawnState>("enemy_0", out var enemy));
            Assert.AreEqual("goblin_01", enemy.EnemyDataSOId);
            Assert.AreEqual(13, enemy.CurrentHP);
            Assert.AreEqual(2, enemy.SpawnPointIndex);
            Assert.IsFalse(enemy.IsDead);
        }

        [Test]
        public void RoundTrip_PreservesMixedSubtypesInSameContainer()
        {
            var src = new SerializableObjectStates();
            src.Set("door_E", new DoorState { Direction = DoorDirection.East, Unlocked = true });
            src.Set("chest_0", new ChestState { Opened = true, LootRolled = { "gold_10", "potion" } });
            src.Set("enemy_0", new EnemySpawnState { CurrentHP = 5, IsDead = true });

            var hydrated = RoundTrip(src);

            Assert.AreEqual(3, hydrated.Count);
            Assert.IsTrue(hydrated.TryGet<DoorState>("door_E", out var d));
            Assert.IsTrue(d.Unlocked);
            Assert.IsTrue(hydrated.TryGet<ChestState>("chest_0", out var c));
            Assert.IsTrue(c.Opened);
            Assert.AreEqual(2, c.LootRolled.Count);
            Assert.IsTrue(hydrated.TryGet<EnemySpawnState>("enemy_0", out var e));
            Assert.IsTrue(e.IsDead);
        }

        [Test]
        public void Set_OverwritesExistingKeyWithoutDuplicating()
        {
            var states = new SerializableObjectStates();
            states.Set("k", new DoorState { Forced = false });
            states.Set("k", new DoorState { Forced = true });

            Assert.AreEqual(1, states.Count);
            Assert.IsTrue(states.TryGet<DoorState>("k", out var d));
            Assert.IsTrue(d.Forced);
        }

        [Test]
        public void Remove_DropsEntryAndReturnsTrue()
        {
            var states = new SerializableObjectStates();
            states.Set("a", new DoorState());
            states.Set("b", new DoorState());

            Assert.IsTrue(states.Remove("a"));
            Assert.AreEqual(1, states.Count);
            Assert.IsFalse(states.ContainsKey("a"));
            Assert.IsTrue(states.ContainsKey("b"));
        }

        [Test]
        public void Remove_UnknownKey_ReturnsFalse()
        {
            var states = new SerializableObjectStates();
            Assert.IsFalse(states.Remove("nope"));
        }

        [Test]
        public void TryGet_WrongSubtype_ReturnsFalse()
        {
            var states = new SerializableObjectStates();
            states.Set("x", new ChestState());

            Assert.IsFalse(states.TryGet<DoorState>("x", out _));
        }

        [Test]
        public void Set_WithEmptyKey_Throws()
        {
            var states = new SerializableObjectStates();
            Assert.Throws<System.ArgumentException>(() => states.Set(string.Empty, new DoorState()));
        }
    }
}