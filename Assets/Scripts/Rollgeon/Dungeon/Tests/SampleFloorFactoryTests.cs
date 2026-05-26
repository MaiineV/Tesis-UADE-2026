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

        private RoomTypeSlot SlotOf(RoomType type) =>
            _result.Layout.Slots.FirstOrDefault(s => s.Type == type);

        [Test]
        public void Create_ReturnsNonNullResult()
        {
            Assert.IsNotNull(_result);
            Assert.IsNotNull(_result.Layout);
            Assert.IsNotNull(_result.AllCreated);
        }

        [Test]
        public void Create_HasSlotsForCombatShopPotionBoss()
        {
            Assert.IsNotNull(SlotOf(RoomType.Combat));
            Assert.IsNotNull(SlotOf(RoomType.Shop));
            Assert.IsNotNull(SlotOf(RoomType.Potion));
            Assert.IsNotNull(SlotOf(RoomType.Boss));
        }

        [Test]
        public void Create_CombatSlotIsRandomBetween5And9()
        {
            var slot = SlotOf(RoomType.Combat);
            Assert.AreEqual(RoomCountMode.Random, slot.Count.Mode);
            Assert.AreEqual(5, slot.Count.Min);
            Assert.AreEqual(9, slot.Count.Max);
        }

        [Test]
        public void Create_ShopAndPotionAndBossAreFixedOne()
        {
            Assert.AreEqual(RoomCountMode.Fixed, SlotOf(RoomType.Shop).Count.Mode);
            Assert.AreEqual(1, SlotOf(RoomType.Shop).Count.Fixed);
            Assert.AreEqual(1, SlotOf(RoomType.Potion).Count.Fixed);
            Assert.AreEqual(1, SlotOf(RoomType.Boss).Count.Fixed);
        }

        [Test]
        public void Create_CombatPoolHasThreeRoomsAllTypeCombat()
        {
            var pool = SlotOf(RoomType.Combat).Pool;
            Assert.AreEqual(3, pool.Count);
            Assert.IsTrue(pool.All(r => r.Type == RoomType.Combat));
        }

        [Test]
        public void Create_ShopPoolHasOneRoomTypeShop()
        {
            var pool = SlotOf(RoomType.Shop).Pool;
            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(RoomType.Shop, pool[0].Type);
        }

        [Test]
        public void Create_PotionPoolHasOneRoomTypePotion()
        {
            var pool = SlotOf(RoomType.Potion).Pool;
            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(RoomType.Potion, pool[0].Type);
        }

        [Test]
        public void Create_BossPoolHasOneRoomTypeBoss()
        {
            var pool = SlotOf(RoomType.Boss).Pool;
            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(RoomType.Boss, pool[0].Type);
        }

        [Test]
        public void Create_CombatRoomsHaveEnemyPools()
        {
            foreach (var room in SlotOf(RoomType.Combat).Pool)
            {
                Assert.IsNotNull(room.EnemyPool,
                    $"Combat room '{room.RoomId}' should have an EnemyPool");
                Assert.IsTrue(room.EnemyPool.Entries.Count > 0,
                    $"Combat room '{room.RoomId}' pool should have entries");
            }
        }

        [Test]
        public void Create_BossRoomHasEnemyPool()
        {
            var boss = SlotOf(RoomType.Boss).Pool[0];
            Assert.IsNotNull(boss.EnemyPool);
            Assert.IsTrue(boss.EnemyPool.Entries.Count > 0);
        }

        [Test]
        public void Create_SpecialNonCombatRoomsHaveNullPools()
        {
            foreach (var room in SlotOf(RoomType.Shop).Pool)
            {
                Assert.IsNull(room.EnemyPool,
                    $"Shop room '{room.RoomId}' should have null EnemyPool");
            }
            foreach (var room in SlotOf(RoomType.Potion).Pool)
            {
                Assert.IsNull(room.EnemyPool,
                    $"Potion room '{room.RoomId}' should have null EnemyPool");
            }
        }

        [Test]
        public void Create_AllRoomIdsAreUnique()
        {
            var allRooms = _result.Layout.Slots.SelectMany(s => s.Pool).ToList();
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
        public void Create_AllCreatedListIsNonNullAndPopulated()
        {
            // 8 enemies + 4 pools + 6 rooms + 1 layout = 19
            Assert.AreEqual(19, _result.AllCreated.Count);
            Assert.IsTrue(_result.AllCreated.All(o => o != null));
        }

        [Test]
        public void Create_DisposeDestroysAllObjects()
        {
            var second = SampleFloorFactory.Create();
            var refs = second.AllCreated.ToList();

            second.Dispose();

            foreach (var obj in refs)
            {
                Assert.IsTrue(obj == null,
                    "Object should be destroyed after Dispose (got non-null ref)");
            }
        }

        [Test]
        public void Create_EnemyPoolsCanRoll()
        {
            var rng = new System.Random(42);
            foreach (var room in SlotOf(RoomType.Combat).Pool)
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
