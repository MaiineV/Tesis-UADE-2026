using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using Rollgeon.Loot;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    [TestFixture]
    public class ChestLootPoolTests
    {
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        [Test]
        public void Roll_ShouldReturnItemsAndGold_WhenGoldChanceSplits()
        {
            // Arrange
            var itemA = NewItem("chest.item.a");
            var itemB = NewItem("chest.item.b");
            var pool = NewPool(ItemRarity.Rare, NewLootPool(itemA, itemB),
                goldChance: 0.3f, goldMin: 5, goldMax: 5);
            var rng = new System.Random(1234);
            var seenItems = new HashSet<string>();
            int goldRolls = 0;

            // Act
            for (int i = 0; i < 300; i++)
            {
                var result = pool.Roll(rng, ItemRarity.Rare);
                if (result.IsGold) goldRolls++;
                else seenItems.Add(result.Item.ItemId);
            }

            // Assert — 300 muestras: salen ambos ítems y también oro.
            Assert.Contains("chest.item.a", new List<string>(seenItems));
            Assert.Contains("chest.item.b", new List<string>(seenItems));
            Assert.Greater(goldRolls, 0);
        }

        [Test]
        public void Roll_ShouldAlwaysReturnGold_WhenGoldChanceIsOne()
        {
            // Arrange
            var pool = NewPool(ItemRarity.Rare, NewLootPool(NewItem("chest.item.a")),
                goldChance: 1f, goldMin: 4, goldMax: 6);
            var rng = new System.Random(5);

            // Act + Assert
            for (int i = 0; i < 50; i++)
            {
                var result = pool.Roll(rng, ItemRarity.Rare);
                Assert.IsTrue(result.IsGold);
                Assert.GreaterOrEqual(result.GoldAmount, 4);
                Assert.LessOrEqual(result.GoldAmount, 6);
            }
        }

        [Test]
        public void Roll_ShouldNeverReturnGold_WhenGoldChanceIsZero_AndPoolHasItems()
        {
            // Arrange
            var item = NewItem("chest.item.only");
            var pool = NewPool(ItemRarity.Uncommon, NewLootPool(item),
                goldChance: 0f, goldMin: 3, goldMax: 3);
            var rng = new System.Random(7);

            // Act + Assert
            for (int i = 0; i < 50; i++)
            {
                var result = pool.Roll(rng, ItemRarity.Uncommon);
                Assert.IsFalse(result.IsGold);
                Assert.AreSame(item, result.Item);
            }
        }

        [Test]
        public void Roll_ShouldDegradeToGold_WhenLootPoolIsEmptyOrMissing()
        {
            // Arrange — un bucket con pool vacío y otro directamente sin pool.
            var emptyPool = NewPool(ItemRarity.Common, NewLootPool(), goldChance: 0f, goldMin: 7, goldMax: 11);
            var nullPool = NewPool(ItemRarity.Legendary, lootPool: null, goldChance: 0f, goldMin: 2, goldMax: 2);
            var rng = new System.Random(99);

            // Act + Assert — sin ítems posibles, siempre oro dentro del rango.
            for (int i = 0; i < 100; i++)
            {
                var fromEmpty = emptyPool.Roll(rng, ItemRarity.Common);
                Assert.IsTrue(fromEmpty.IsGold);
                Assert.GreaterOrEqual(fromEmpty.GoldAmount, 7);
                Assert.LessOrEqual(fromEmpty.GoldAmount, 11);

                var fromNull = nullPool.Roll(rng, ItemRarity.Legendary);
                Assert.IsTrue(fromNull.IsGold);
                Assert.AreEqual(2, fromNull.GoldAmount);
            }
        }

        [Test]
        public void Roll_ShouldReturnZeroGold_WhenTierHasNoBucket()
        {
            // Arrange
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);
            var rng = new System.Random(1);

            // Act
            var result = pool.Roll(rng, ItemRarity.Legendary);

            // Assert
            Assert.IsTrue(result.IsGold);
            Assert.AreEqual(0, result.GoldAmount);
        }

        [Test]
        public void GetPoolPreview_ShouldReturnOnlyValidItems_InAuthoredOrder()
        {
            // Arrange
            var itemA = NewItem("chest.item.a");
            var itemB = NewItem("chest.item.b");
            var lootPool = NewLootPool(itemA, null, itemB);
            var pool = NewPool(ItemRarity.Legendary, lootPool, goldChance: 0.15f, goldMin: 1, goldMax: 1);

            // Act
            var preview = pool.GetPoolPreview(ItemRarity.Legendary);

            // Assert
            Assert.AreEqual(2, preview.Count);
            Assert.AreSame(itemA, preview[0]);
            Assert.AreSame(itemB, preview[1]);
        }

        [Test]
        public void GetPoolPreview_ShouldReturnEmpty_WhenBucketOrPoolMissing()
        {
            // Arrange — sin bucket para Rare; bucket Legendary sin pool.
            var pool = NewPool(ItemRarity.Legendary, lootPool: null, goldChance: 0f, goldMin: 1, goldMax: 1);

            // Act + Assert
            Assert.AreEqual(0, pool.GetPoolPreview(ItemRarity.Rare).Count);
            Assert.AreEqual(0, pool.GetPoolPreview(ItemRarity.Legendary).Count);
        }

        private ChestLootPoolSO NewPool(
            ItemRarity tier, LootPoolSO lootPool, float goldChance, int goldMin, int goldMax)
        {
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);
            pool.Buckets.Add(new ChestLootBucket
            {
                Tier = tier,
                Pool = lootPool,
                GoldChance = goldChance,
                GoldMin = goldMin,
                GoldMax = goldMax,
            });
            return pool;
        }

        private LootPoolSO NewLootPool(params ItemSO[] items)
        {
            var lootPool = ScriptableObject.CreateInstance<LootPoolSO>();
            _assets.Add(lootPool);
            lootPool.Items = new List<ItemSO>(items);
            return lootPool;
        }

        private ItemSO NewItem(string itemId)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = itemId;
            _assets.Add(item);
            return item;
        }
    }
}
