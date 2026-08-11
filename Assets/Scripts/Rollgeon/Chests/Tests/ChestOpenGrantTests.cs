using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    [TestFixture]
    public class ChestOpenGrantTests
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
        public void Grant_ShouldDeliverItemToInventory_WhenRollIsItemAndInventoryAccepts()
        {
            // Arrange
            var item = NewItem("chest.item.sword");
            var inventory = new FakeInventoryService();
            var economy = new FakeEconomyService();

            // Act
            var result = ChestOpenGrant.Grant(
                inventory, economy, ChestLootResult.ForItem(item), new ChestTierDef());

            // Assert
            Assert.IsFalse(result.IsGold);
            Assert.AreSame(item, result.Item);
            Assert.IsFalse(result.WasInventoryFullFallback);
            Assert.AreEqual(1, inventory.Added.Count);
            Assert.AreEqual(0, economy.CurrentGold);
        }

        [Test]
        public void Grant_ShouldAddGoldToEconomy_WhenRollIsGold()
        {
            // Arrange
            var inventory = new FakeInventoryService();
            var economy = new FakeEconomyService();

            // Act
            var result = ChestOpenGrant.Grant(
                inventory, economy, ChestLootResult.ForGold(25), new ChestTierDef());

            // Assert
            Assert.IsTrue(result.IsGold);
            Assert.AreEqual(25, result.GoldAmount);
            Assert.IsFalse(result.WasInventoryFullFallback);
            Assert.AreEqual(25, economy.CurrentGold);
            Assert.AreEqual(0, inventory.Added.Count);
        }

        [Test]
        public void Grant_ShouldFallBackToTierGold_WhenInventoryRejectsItem()
        {
            // Arrange
            var item = NewItem("chest.item.potion");
            var inventory = new FakeInventoryService { RejectAdds = true };
            var economy = new FakeEconomyService();
            var tierDef = new ChestTierDef { FallbackGold = 12 };

            // Act
            var result = ChestOpenGrant.Grant(
                inventory, economy, ChestLootResult.ForItem(item), tierDef);

            // Assert
            Assert.IsTrue(result.IsGold);
            Assert.AreEqual(12, result.GoldAmount);
            Assert.IsTrue(result.WasInventoryFullFallback);
            Assert.AreEqual(12, economy.CurrentGold);
            Assert.AreEqual(0, inventory.Added.Count);
        }

        [Test]
        public void Grant_ShouldFallBackToGold_WhenInventoryServiceIsMissing()
        {
            // Arrange
            var item = NewItem("chest.item.orphan");
            var economy = new FakeEconomyService();
            var tierDef = new ChestTierDef { FallbackGold = 8 };

            // Act
            var result = ChestOpenGrant.Grant(
                inventory: null, economy, ChestLootResult.ForItem(item), tierDef);

            // Assert
            Assert.IsTrue(result.IsGold);
            Assert.IsTrue(result.WasInventoryFullFallback);
            Assert.AreEqual(8, economy.CurrentGold);
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
