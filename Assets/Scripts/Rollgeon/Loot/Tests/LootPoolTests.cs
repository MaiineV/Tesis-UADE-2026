using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Loot.Tests
{
    [TestFixture]
    public class LootPoolTests
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
        public void RollItem_ShouldNeverPickCategory_WhenItsWeightIsZero()
        {
            // Arrange
            var common = NewItem("loot.common", ItemRarity.Common);
            var rare = NewItem("loot.rare", ItemRarity.Rare);
            var pool = NewPool(new[] { common, rare },
                new RarityWeights { Common = 1f, Uncommon = 1f, Rare = 0f, Legendary = 1f });
            var rng = new System.Random(42);

            // Act + Assert — Rare pesa 0: jamás sale por más que esté en la lista.
            for (int i = 0; i < 200; i++)
            {
                Assert.AreSame(common, pool.RollItem(rng));
            }
        }

        [Test]
        public void RollItem_ShouldFavorHeavyCategory_OverLightOne()
        {
            // Arrange — Common pesa 90, Legendary 10.
            var common = NewItem("loot.common", ItemRarity.Common);
            var legendary = NewItem("loot.legendary", ItemRarity.Legendary);
            var pool = NewPool(new[] { common, legendary },
                new RarityWeights { Common = 90f, Uncommon = 0f, Rare = 0f, Legendary = 10f });
            var rng = new System.Random(1234);
            int commonCount = 0, legendaryCount = 0;

            // Act
            for (int i = 0; i < 500; i++)
            {
                var rolled = pool.RollItem(rng);
                if (rolled == common) commonCount++;
                else if (rolled == legendary) legendaryCount++;
            }

            // Assert — ambos salen, y el pesado domina con margen amplio (esperado
            // 450/50; el umbral 3x tolera varianza del seed sin flakiness).
            Assert.Greater(legendaryCount, 0);
            Assert.Greater(commonCount, legendaryCount * 3);
        }

        [Test]
        public void RollItem_ShouldIgnoreWeightedCategory_WhenItHasNoItems()
        {
            // Arrange — Legendary pesa 100 pero no hay ítems Legendary en la lista:
            // la categoría se ignora y renormaliza sobre las pobladas.
            var common = NewItem("loot.common", ItemRarity.Common);
            var pool = NewPool(new[] { common },
                new RarityWeights { Common = 1f, Uncommon = 0f, Rare = 0f, Legendary = 100f });
            var rng = new System.Random(7);

            // Act + Assert
            for (int i = 0; i < 100; i++)
            {
                Assert.AreSame(common, pool.RollItem(rng));
            }
        }

        [Test]
        public void RollItem_ShouldFallBackToUniform_WhenPopulatedWeightsTotalZero()
        {
            // Arrange — las únicas categorías con ítems pesan 0 ⇒ uniforme entre todos.
            var itemA = NewItem("loot.a", ItemRarity.Common);
            var itemB = NewItem("loot.b", ItemRarity.Rare);
            var pool = NewPool(new[] { itemA, itemB },
                new RarityWeights { Common = 0f, Uncommon = 5f, Rare = 0f, Legendary = 5f });
            var rng = new System.Random(21);
            var seen = new HashSet<ItemSO>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var rolled = pool.RollItem(rng);
                Assert.IsNotNull(rolled);
                seen.Add(rolled);
            }

            // Assert
            Assert.IsTrue(seen.Contains(itemA));
            Assert.IsTrue(seen.Contains(itemB));
        }

        [Test]
        public void RollItem_ShouldReturnNull_WhenPoolIsEmpty()
        {
            // Arrange
            var empty = NewPool(new ItemSO[0], new RarityWeights());
            var withHoles = NewPool(new ItemSO[] { null, null }, new RarityWeights());
            var rng = new System.Random(3);

            // Act + Assert
            Assert.IsNull(empty.RollItem(rng));
            Assert.IsNull(withHoles.RollItem(rng));
        }

        [Test]
        public void RollItem_ShouldBeDeterministic_ForSameSeed()
        {
            // Arrange
            var items = new[]
            {
                NewItem("loot.a", ItemRarity.Common),
                NewItem("loot.b", ItemRarity.Uncommon),
                NewItem("loot.c", ItemRarity.Rare),
                NewItem("loot.d", ItemRarity.Legendary),
            };
            var weights = new RarityWeights { Common = 40f, Uncommon = 30f, Rare = 20f, Legendary = 10f };
            var pool = NewPool(items, weights);

            // Act — misma seed, dos secuencias.
            var first = RollSequence(pool, seed: 777, count: 50);
            var second = RollSequence(pool, seed: 777, count: 50);

            // Assert
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void GetPreview_ShouldReturnValidItems_InAuthoredOrder()
        {
            // Arrange
            var itemA = NewItem("loot.a", ItemRarity.Common);
            var itemB = NewItem("loot.b", ItemRarity.Legendary);
            var pool = NewPool(new[] { itemA, null, itemB }, new RarityWeights());

            // Act
            var preview = pool.GetPreview();

            // Assert
            Assert.AreEqual(2, preview.Count);
            Assert.AreSame(itemA, preview[0]);
            Assert.AreSame(itemB, preview[1]);
        }

        private List<ItemSO> RollSequence(LootPoolSO pool, int seed, int count)
        {
            var rng = new System.Random(seed);
            var sequence = new List<ItemSO>(count);
            for (int i = 0; i < count; i++) sequence.Add(pool.RollItem(rng));
            return sequence;
        }

        private LootPoolSO NewPool(ItemSO[] items, RarityWeights weights)
        {
            var pool = ScriptableObject.CreateInstance<LootPoolSO>();
            _assets.Add(pool);
            pool.Items = new List<ItemSO>(items);
            pool.Weights = weights;
            return pool;
        }

        private ItemSO NewItem(string itemId, ItemRarity rarity)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = itemId;
            item.Rarity = rarity;
            _assets.Add(item);
            return item;
        }
    }
}
