using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Shop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// Tests del gating de pools por meta-progresión (#164): ítems de tienda y
    /// salas gateadas quedan fuera de los rolls hasta desbloquearse, y el gate
    /// degrada a "todo disponible" sin servicio registrado.
    /// </summary>
    [TestFixture]
    public class PoolGatingTests
    {
        private MetaProgressionService _meta;
        private InMemoryMetaSaveStore _store;
        private UnlockCatalogSO _catalog;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();

            _store = new InMemoryMetaSaveStore();
            _catalog = ScriptableObject.CreateInstance<UnlockCatalogSO>();
            _assets.Add(_catalog);

            _meta = new MetaProgressionService();
            _meta.ConfigureForTests(_store, _catalog);
            ServiceLocator.AddService<IMetaProgressionService>(_meta, ServiceScope.Global);
        }

        [TearDown]
        public void Teardown()
        {
            TypedEvent<UnlockAchievedPayload>.Clear();
            ServiceLocator.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private UnlockDefinitionSO Gate(UnlockableCategory category, string targetId)
        {
            var def = ScriptableObject.CreateInstance<UnlockDefinitionSO>();
            def.UnlockId = $"unlock.{category}.{targetId}".ToLowerInvariant();
            def.Category = category;
            def.TargetId = targetId;
            _assets.Add(def);

            _catalog.AddEntry(def);
            _meta.ConfigureForTests(_store, _catalog);
            return def;
        }

        private ShopItemDef NewShopItem(string itemId)
        {
            var item = ScriptableObject.CreateInstance<ShopItemDef>();
            item.ItemId = itemId;
            item.DisplayName = itemId;
            _assets.Add(item);
            return item;
        }

        private ShopPoolSO NewShopPool(params ShopItemDef[] items)
        {
            var pool = ScriptableObject.CreateInstance<ShopPoolSO>();
            foreach (var item in items)
            {
                pool.Items.Add(new WeightedShopItem { Item = item, Weight = 1f, BasePrice = 10 });
            }
            _assets.Add(pool);
            return pool;
        }

        // ── Shop pool ───────────────────────────────────────────

        [Test]
        public void ShopRoll_GatedItem_NeverRolled()
        {
            var baseItem = NewShopItem("item.base");
            var gatedItem = NewShopItem("item.gated");
            var pool = NewShopPool(baseItem, gatedItem);
            Gate(UnlockableCategory.ShopItem, "item.gated");

            var rng = new System.Random(1234);
            for (int i = 0; i < 50; i++)
            {
                var result = pool.Roll(rng, floorDepth: 0);
                Assert.AreEqual("item.base", result.Item.EntryId,
                    "Un ítem gateado no debe entrar al pool de la tienda");
            }
        }

        [Test]
        public void ShopRoll_GatedItemUnlocked_BecomesEligible()
        {
            var gatedItem = NewShopItem("item.gated");
            var pool = NewShopPool(gatedItem);
            var def = Gate(UnlockableCategory.ShopItem, "item.gated");

            var rng = new System.Random(1234);
            Assert.IsNull(pool.Roll(rng, floorDepth: 0).Item, "Bloqueado: el pool queda vacío");

            _meta.TryUnlock(def, duringRun: false);

            var result = pool.Roll(rng, floorDepth: 0);
            Assert.IsNotNull(result.Item);
            Assert.AreEqual("item.gated", result.Item.EntryId);
        }

        [Test]
        public void ShopRoll_NoServiceRegistered_GateDegradesToAvailable()
        {
            var gatedItem = NewShopItem("item.gated");
            var pool = NewShopPool(gatedItem);
            Gate(UnlockableCategory.ShopItem, "item.gated");
            ServiceLocator.Clear(); // sin IMetaProgressionService

            var result = pool.Roll(new System.Random(1), floorDepth: 0);

            Assert.IsNotNull(result.Item, "Sin servicio el gate no debe filtrar (tests/escenas sueltas)");
        }

        // ── Floor generation ────────────────────────────────────

        [Test]
        public void BuildPoolsByType_GatedRoom_ExcludedFromGeneration()
        {
            var baseRoom = ScriptableObject.CreateInstance<RoomSO>();
            baseRoom.RoomId = "Shop01";
            baseRoom.Type = RoomType.Shop;
            _assets.Add(baseRoom);

            var gatedRoom = ScriptableObject.CreateInstance<RoomSO>();
            gatedRoom.RoomId = "Casino01";
            gatedRoom.Type = RoomType.Shop;
            _assets.Add(gatedRoom);

            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.Slots = new List<RoomTypeSlot>
            {
                new RoomTypeSlot
                {
                    Type = RoomType.Shop,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                    Pool = new List<RoomSO> { baseRoom, gatedRoom },
                },
            };
            _assets.Add(layout);

            Gate(UnlockableCategory.SpecialRoom, "Casino01");

            var pools = FloorTopologyPlanner.BuildPoolsByType(layout);

            CollectionAssert.Contains(pools[RoomType.Shop], baseRoom);
            CollectionAssert.DoesNotContain(pools[RoomType.Shop], gatedRoom);
        }

        [Test]
        public void BuildPoolsByType_UnlockedRoom_EntersGeneration()
        {
            var gatedRoom = ScriptableObject.CreateInstance<RoomSO>();
            gatedRoom.RoomId = "Casino01";
            gatedRoom.Type = RoomType.Shop;
            _assets.Add(gatedRoom);

            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.Slots = new List<RoomTypeSlot>
            {
                new RoomTypeSlot
                {
                    Type = RoomType.Shop,
                    Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                    Pool = new List<RoomSO> { gatedRoom },
                },
            };
            _assets.Add(layout);

            var def = Gate(UnlockableCategory.SpecialRoom, "Casino01");
            _meta.TryUnlock(def, duringRun: false);

            var pools = FloorTopologyPlanner.BuildPoolsByType(layout);

            CollectionAssert.Contains(pools[RoomType.Shop], gatedRoom);
        }
    }
}
