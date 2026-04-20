using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class SampleFloorFactoryTests
    {
        private SampleFloorFactory.Result _result;

        [SetUp]
        public void SetUp()
        {
            _result = SampleFloorFactory.Create();
        }

        [TearDown]
        public void TearDown()
        {
            _result?.Dispose();
            _result = null;
        }

        [Test]
        public void Create_ReturnsNonNullResult()
        {
            Assert.IsNotNull(_result);
            Assert.IsNotNull(_result.Layout);
            Assert.IsNotNull(_result.AllCreated);
        }

        [Test]
        public void Create_LayoutHasExpectedRoomCounts()
        {
            Assert.AreEqual(8,  _result.Layout.RoomCountMin);
            Assert.AreEqual(12, _result.Layout.RoomCountMax);
        }

        [Test]
        public void Create_HasThreeCombatRooms()
        {
            Assert.AreEqual(3, _result.Layout.CombatRooms.Count);
            Assert.IsTrue(_result.Layout.CombatRooms.All(r => r.Type == RoomType.Combat));
        }

        [Test]
        public void Create_HasOneShopRoom()
        {
            Assert.AreEqual(1, _result.Layout.ShopRooms.Count);
            Assert.AreEqual(RoomType.Shop, _result.Layout.ShopRooms[0].Type);
        }

        [Test]
        public void Create_HasOnePotionRoom()
        {
            Assert.AreEqual(1, _result.Layout.PotionRooms.Count);
            Assert.AreEqual(RoomType.Potion, _result.Layout.PotionRooms[0].Type);
        }

        [Test]
        public void Create_HasTwoBossCandidates()
        {
            Assert.AreEqual(2, _result.Layout.BossCandidates.Count);
        }

        [Test]
        public void Create_CombatRoomsHaveEnemyPools()
        {
            foreach (var room in _result.Layout.CombatRooms)
            {
                Assert.IsNotNull(room.EnemyPool,
                    $"Combat room '{room.RoomId}' should have an EnemyPool");
                Assert.IsTrue(room.EnemyPool.Entries.Count > 0,
                    $"Combat room '{room.RoomId}' pool should have entries");
            }
        }

        [Test]
        public void Create_SpecialRoomsHaveNullPools()
        {
            foreach (var room in _result.Layout.ShopRooms)
            {
                Assert.IsNull(room.EnemyPool,
                    $"Shop room '{room.RoomId}' should have null EnemyPool");
            }
            foreach (var room in _result.Layout.PotionRooms)
            {
                Assert.IsNull(room.EnemyPool,
                    $"Potion room '{room.RoomId}' should have null EnemyPool");
            }
        }

        [Test]
        public void Create_AllRoomIdsAreUnique()
        {
            var allRooms = _result.Layout.CombatRooms
                .Concat(_result.Layout.ShopRooms)
                .Concat(_result.Layout.PotionRooms)
                .ToList();

            var ids = allRooms.Select(r => r.RoomId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(),
                "All room ids must be unique");
        }

        [Test]
        public void Create_AllEnemyIdsAreUnique()
        {
            var enemies = _result.AllCreated
                .OfType<Entities.EnemyDataSO>()
                .ToList();

            var ids = enemies.Select(e => e.EntityId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(),
                "All enemy EntityIds must be unique");
        }

        [Test]
        public void Create_AllCreatedListContainsAllInstances()
        {
            // 8 enemies + 3 pools + 5 rooms + 1 layout = 17
            Assert.AreEqual(17, _result.AllCreated.Count);
            Assert.IsTrue(_result.AllCreated.All(o => o != null));
        }

        [Test]
        public void Create_DisposeDestroysAllObjects()
        {
            var second = SampleFloorFactory.Create();
            var refs = second.AllCreated.ToList();

            second.Dispose();

            // Unity null check — destroyed objects compare == null
            foreach (var obj in refs)
            {
                Assert.IsTrue(obj == null,
                    $"Object '{obj?.name ?? "(destroyed)"}' should be destroyed after Dispose");
            }
        }

        [Test]
        public void Create_BossCandidatesHaveHighHP()
        {
            foreach (var boss in _result.Layout.BossCandidates)
            {
                Assert.GreaterOrEqual(boss.BaseHP, 50,
                    $"Boss '{boss.EntityId}' should have high HP (got {boss.BaseHP})");
            }
        }

        [Test]
        public void Create_EnemyPoolsCanRoll()
        {
            var rng = new System.Random(42);
            foreach (var room in _result.Layout.CombatRooms)
            {
                var spawns = room.EnemyPool.RollForSpawns(3, rng);
                Assert.AreEqual(3, spawns.Count,
                    $"Pool in '{room.RoomId}' should roll 3 enemies");
                Assert.IsTrue(spawns.All(e => e != null),
                    $"All rolled enemies from '{room.RoomId}' should be non-null");
            }
        }
    }
}
