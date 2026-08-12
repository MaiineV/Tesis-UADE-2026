using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
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
        public void Roll_ShouldReturnEveryItemAndGold_WhenSampledUniformly()
        {
            // Arrange
            var itemA = NewItem("chest.item.a");
            var itemB = NewItem("chest.item.b");
            var pool = NewPool(ItemRarity.Rare, new[] { itemA, itemB }, goldMin: 5, goldMax: 5);
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

            // Assert — con 300 muestras uniformes sobre 3 slots, todos aparecen.
            Assert.Contains("chest.item.a", new List<string>(seenItems));
            Assert.Contains("chest.item.b", new List<string>(seenItems));
            Assert.Greater(goldRolls, 0);
        }

        [Test]
        public void Roll_ShouldReturnGoldWithinRange_WhenGoldSlotRolls()
        {
            // Arrange
            var pool = NewPool(ItemRarity.Common, new ItemSO[0], goldMin: 7, goldMax: 11);
            var rng = new System.Random(99);

            // Act + Assert — bucket sin ítems ⇒ siempre oro, siempre dentro del rango.
            for (int i = 0; i < 100; i++)
            {
                var result = pool.Roll(rng, ItemRarity.Common);
                Assert.IsTrue(result.IsGold);
                Assert.GreaterOrEqual(result.GoldAmount, 7);
                Assert.LessOrEqual(result.GoldAmount, 11);
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
        public void Roll_ShouldSkipNullItems_WhenBucketHasHoles()
        {
            // Arrange
            var item = NewItem("chest.item.only");
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);
            pool.Buckets.Add(new ChestLootBucket
            {
                Tier = ItemRarity.Uncommon,
                Items = { null, item, null },
                GoldMin = 3,
                GoldMax = 3
            });
            var rng = new System.Random(7);

            // Act — 2 slots efectivos (ítem + oro); ninguno debe ser null-item.
            for (int i = 0; i < 60; i++)
            {
                var result = pool.Roll(rng, ItemRarity.Uncommon);
                if (!result.IsGold)
                {
                    // Assert
                    Assert.AreSame(item, result.Item);
                }
            }
        }

        [Test]
        public void GetPoolPreview_ShouldReturnOnlyValidItems_InAuthoredOrder()
        {
            // Arrange
            var itemA = NewItem("chest.item.a");
            var itemB = NewItem("chest.item.b");
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);
            pool.Buckets.Add(new ChestLootBucket
            {
                Tier = ItemRarity.Legendary,
                Items = { itemA, null, itemB }
            });

            // Act
            var preview = pool.GetPoolPreview(ItemRarity.Legendary);

            // Assert
            Assert.AreEqual(2, preview.Count);
            Assert.AreSame(itemA, preview[0]);
            Assert.AreSame(itemB, preview[1]);
        }

        [Test]
        public void GetPoolPreview_ShouldReturnEmpty_WhenTierHasNoBucket()
        {
            // Arrange
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);

            // Act
            var preview = pool.GetPoolPreview(ItemRarity.Rare);

            // Assert
            Assert.IsNotNull(preview);
            Assert.AreEqual(0, preview.Count);
        }

        private ChestLootPoolSO NewPool(ItemRarity tier, ItemSO[] items, int goldMin, int goldMax)
        {
            var pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
            _assets.Add(pool);
            var bucket = new ChestLootBucket { Tier = tier, GoldMin = goldMin, GoldMax = goldMax };
            bucket.Items.Clear();
            bucket.Items.AddRange(items);
            pool.Buckets.Add(bucket);
            return pool;
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
