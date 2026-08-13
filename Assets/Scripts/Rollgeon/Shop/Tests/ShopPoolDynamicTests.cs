using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Shop.Tests
{
    /// <summary>
    /// Pool de la tienda item-first: entry garantizada (poción, slot 0) y roll de
    /// los slots restantes desde <see cref="ShopPoolSO.Items"/> (cada entry es un
    /// <see cref="ItemSO"/>; precio = BasePrice), con exclude de lo ya roleado y
    /// filtro por MinFloorDepth. Sin IMetaProgressionService registrado el gate
    /// degrada a "todo disponible" (ver PoolGatingTests).
    /// </summary>
    [TestFixture]
    public class ShopPoolDynamicTests
    {
        private readonly List<Object> _assets = new List<Object>();
        private System.Random _rng;

        [SetUp]
        public void Setup()
        {
            _rng = new System.Random(1234);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ---------------- Guaranteed ----------------

        [Test]
        public void TryGetGuaranteed_WithEntry_ReturnsItemAndBasePrice()
        {
            var pool = NewPool();
            var potion = NewItem("item.pocion");
            pool.Guaranteed = new WeightedShopItem { Item = potion, Weight = 1f, BasePrice = 8 };

            bool found = pool.TryGetGuaranteed(out var result);

            Assert.IsTrue(found);
            Assert.AreEqual("item.pocion", result.Item.EntryId);
            Assert.AreEqual(8, result.BasePrice);
        }

        [Test]
        public void TryGetGuaranteed_Unset_ReturnsFalse()
        {
            var pool = NewPool();

            Assert.IsFalse(pool.TryGetGuaranteed(out _));
        }

        // ---------------- RollDynamic ----------------

        [Test]
        public void RollDynamic_RollsFromItems_PriceComesFromBasePrice()
        {
            var pool = NewPool();
            var item = NewItem("item.only");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = item, Weight = 1f, BasePrice = 30 },
            };

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.AreEqual("item.only", result.Item.EntryId);
            Assert.AreEqual(30, result.BasePrice);
        }

        [Test]
        public void RollDynamic_ExcludesAlreadyRolledItems()
        {
            var pool = NewPool();
            var first = NewItem("item.a");
            var second = NewItem("item.b");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = first, Weight = 1f, BasePrice = 20 },
                new WeightedShopItem { Item = second, Weight = 1f, BasePrice = 25 },
            };

            var exclude = new List<IShopRewardEntry> { (IShopRewardEntry)first };
            var result = pool.RollDynamic(_rng, floorDepth: 0, exclude);

            Assert.AreEqual("item.b", result.Item.EntryId,
                "Un item ya roleado en esta tienda no debe repetirse mientras haya variedad.");
        }

        [Test]
        public void RollDynamic_EmptyItems_ReturnsDefault()
        {
            var pool = NewPool();

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.IsNull(result.Item);
        }

        [Test]
        public void RollDynamic_AllItemsExcluded_FallsBackIgnoringExclude()
        {
            var pool = NewPool();
            var only = NewItem("item.only");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = only, Weight = 1f, BasePrice = 12 },
            };

            var exclude = new List<IShopRewardEntry> { (IShopRewardEntry)only };
            var result = pool.RollDynamic(_rng, floorDepth: 0, exclude);

            Assert.AreEqual("item.only", result.Item.EntryId,
                "Con todo excluido, el fallback ignora el exclude — mejor un duplicado que un slot vacío.");
        }

        [Test]
        public void RollDynamic_MinFloorDepth_FiltersItems()
        {
            var pool = NewPool();
            var late = NewItem("item.late");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = late, Weight = 1f, BasePrice = 40, MinFloorDepth = 3 },
            };

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.IsNull(result.Item, "Un item con MinFloorDepth mayor al piso no debe rolear.");
        }

        // ---------------- helpers ----------------

        private ShopPoolSO NewPool()
        {
            var pool = ScriptableObject.CreateInstance<ShopPoolSO>();
            _assets.Add(pool);
            return pool;
        }

        private ItemSO NewItem(string itemId)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = itemId;
            item.DisplayName = itemId;
            _assets.Add(item);
            return item;
        }
    }
}
