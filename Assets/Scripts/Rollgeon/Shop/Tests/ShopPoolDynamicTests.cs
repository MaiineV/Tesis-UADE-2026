using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Combos;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Shop.Tests
{
    /// <summary>
    /// Pool dinámico de la tienda: entry garantizada (poción, slot 0), roll de
    /// combo passives desde <see cref="ComboPassivePoolSO"/> con precio =
    /// ShopCost, y fallback a las entries manuales. Sin IMetaProgressionService
    /// registrado el gate degrada a "todo disponible" (ver PoolGatingTests).
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
            var potion = NewItemDef("item.pocion");
            pool.Guaranteed = new WeightedShopItem { Item = potion, Weight = 1f, BasePrice = 8 };

            bool found = pool.TryGetGuaranteed(out var result);

            Assert.IsTrue(found);
            Assert.AreSame(potion, result.Item);
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
        public void RollDynamic_UsesPassivePool_PriceComesFromShopCost()
        {
            var pool = NewPool();
            var passive = NewPassive("combo.gold_on_ladder", shopCost: 30);
            pool.PassivePool = NewPassivePool(passive);

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.AreSame(passive, result.Item);
            Assert.AreEqual(30, result.BasePrice);
        }

        [Test]
        public void RollDynamic_ExcludesAlreadyRolledPassives()
        {
            var pool = NewPool();
            var first = NewPassive("combo.a", shopCost: 20);
            var second = NewPassive("combo.b", shopCost: 25);
            pool.PassivePool = NewPassivePool(first, second);

            var exclude = new List<IShopRewardEntry> { first };
            var result = pool.RollDynamic(_rng, floorDepth: 0, exclude);

            Assert.AreSame(second, result.Item,
                "Una pasiva ya roleada en esta tienda no debe repetirse mientras haya variedad.");
        }

        [Test]
        public void RollDynamic_EmptyPassivePool_FallsBackToManualItems()
        {
            var pool = NewPool();
            var manual = NewItemDef("item.manual");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = manual, Weight = 1f, BasePrice = 12 },
            };

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.AreSame(manual, result.Item);
            Assert.AreEqual(12, result.BasePrice);
        }

        [Test]
        public void RollDynamic_AllPassivesExcluded_FallsBackToManualItems()
        {
            var pool = NewPool();
            var passive = NewPassive("combo.only", shopCost: 20);
            pool.PassivePool = NewPassivePool(passive);
            var manual = NewItemDef("item.manual");
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = manual, Weight = 1f, BasePrice = 12 },
            };

            var exclude = new List<IShopRewardEntry> { passive };
            var result = pool.RollDynamic(_rng, floorDepth: 0, exclude);

            Assert.AreSame(manual, result.Item);
        }

        [Test]
        public void RollDynamic_MinFloorDepth_FiltersPassives()
        {
            var pool = NewPool();
            var late = NewPassive("combo.late", shopCost: 40);
            var passivePool = NewPassivePool(late);
            passivePool.Entries[0].MinFloorDepth = 3;
            pool.PassivePool = passivePool;

            var result = pool.RollDynamic(_rng, floorDepth: 0);

            Assert.IsNull(result.Item, "Una pasiva con MinFloorDepth mayor al piso no debe rolear.");
        }

        // ---------------- helpers ----------------

        private ShopPoolSO NewPool()
        {
            var pool = ScriptableObject.CreateInstance<ShopPoolSO>();
            _assets.Add(pool);
            return pool;
        }

        private ShopItemDef NewItemDef(string itemId)
        {
            var def = ScriptableObject.CreateInstance<ShopItemDef>();
            def.ItemId = itemId;
            def.DisplayName = itemId;
            _assets.Add(def);
            return def;
        }

        private ComboPassiveSO NewPassive(string upgradeId, int shopCost)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            SetPrivateField(passive, "_upgradeId", upgradeId);
            SetPrivateField(passive, "_shopCost", shopCost);
            _assets.Add(passive);
            return passive;
        }

        private ComboPassivePoolSO NewPassivePool(params ComboPassiveSO[] passives)
        {
            var pool = ScriptableObject.CreateInstance<ComboPassivePoolSO>();
            pool.Entries = new List<WeightedComboPassive>();
            foreach (var p in passives)
            {
                pool.Entries.Add(new WeightedComboPassive { Passive = p, Weight = 1f, MinFloorDepth = 0 });
            }
            _assets.Add(pool);
            return pool;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Assert.Fail($"Field '{fieldName}' no encontrado en {target.GetType().Name} ni sus bases.");
        }
    }
}
