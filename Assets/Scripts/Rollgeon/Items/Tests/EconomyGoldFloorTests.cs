using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Economy;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Tarjeta de Crédito (Feature#0074): piso de oro en <see cref="EconomyService"/> y su
    /// lifecycle vía <see cref="ItemSO.GoldFloor"/> en el inventario.
    /// </summary>
    [TestFixture]
    public class EconomyGoldFloorTests
    {
        private EconomyService _economy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            _economy = new EconomyService(startingGold: 10);
        }

        [TearDown]
        public void TearDown()
        {
            _economy?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
        }

        [Test]
        public void WithoutFloor_SpendStopsAtZero()
        {
            Assert.AreEqual(0, _economy.MinGold);
            Assert.IsFalse(_economy.CanAfford(11));
            Assert.IsFalse(_economy.Spend(11));
            Assert.IsTrue(_economy.Spend(10));
            Assert.AreEqual(0, _economy.CurrentGold);
        }

        [Test]
        public void WithFloor_SpendGoesIntoDebt_UpToTheFloor()
        {
            _economy.SetGoldFloor("tarjeta.credito", -30);

            Assert.AreEqual(-30, _economy.MinGold);
            Assert.IsTrue(_economy.CanAfford(40));
            Assert.IsTrue(_economy.Spend(40));
            Assert.AreEqual(-30, _economy.CurrentGold);

            // Ya en el piso: ni un oro más.
            Assert.IsFalse(_economy.CanAfford(1));
            Assert.IsFalse(_economy.Spend(1));
            Assert.AreEqual(-30, _economy.CurrentGold);
        }

        [Test]
        public void SeveralFloors_TheLowestWins_AndPositiveFloorsAreIgnored()
        {
            _economy.SetGoldFloor("a", -10);
            _economy.SetGoldFloor("b", -30);
            _economy.SetGoldFloor("c", 50);
            Assert.AreEqual(-30, _economy.MinGold);

            _economy.ClearGoldFloor("b");
            Assert.AreEqual(-10, _economy.MinGold);
        }

        [Test]
        public void ClearFloor_DoesNotConfiscateDebt_ButBlocksFurtherSpending()
        {
            _economy.SetGoldFloor("tarjeta.credito", -30);
            _economy.Spend(30); // -20

            _economy.ClearGoldFloor("tarjeta.credito");

            Assert.AreEqual(-20, _economy.CurrentGold, "la deuda queda");
            Assert.IsFalse(_economy.Spend(1));
            _economy.Add(25);
            Assert.AreEqual(5, _economy.CurrentGold, "el próximo Add salda la deuda");
        }

        [Test]
        public void Restore_KeepsNegativeGold()
        {
            _economy.RestoreState(-12);
            Assert.AreEqual(-12, _economy.CurrentGold);
        }

        [Test]
        public void Inventory_RegistersAndClearsTheFloorWithTheItem()
        {
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
            var inventory = new InventoryService(null, 4);
            var card = ScriptableObject.CreateInstance<ItemSO>();
            card.ItemId = "tarjeta.credito";
            card.Type = ItemType.Passive;
            card.GoldFloor = -30;

            try
            {
                inventory.AddItem(card);
                Assert.AreEqual(-30, _economy.MinGold);

                inventory.RemoveItem(card.ItemId);
                Assert.AreEqual(0, _economy.MinGold);
            }
            finally
            {
                inventory.Dispose();
                Object.DestroyImmediate(card);
            }
        }

        [Test]
        public void EgoistaReader_TreatsDebtAsZeroGold()
        {
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
            _economy.SetGoldFloor("tarjeta.credito", -30);
            _economy.Spend(30);

            var reader = new Rollgeon.Upgrades.Dice.Readers.ReadCurrentGoldSqrtScaled();
            Assert.AreEqual(0, reader.Read(new Rollgeon.Effects.EffectContext()));
        }
    }
}
