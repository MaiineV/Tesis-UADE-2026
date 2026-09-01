using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Lifecycle del multiplicador de peso de malditos (Moneda Maldita): se registra
    /// al entrar el item al inventario, se desregistra al perderlo, y un item con
    /// multiplicador 1 no ensucia el registry.
    /// </summary>
    [TestFixture]
    public class InventoryEnchantmentWeightModifierTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private InventoryService _inventory;
        private EnchantmentWeightModifierService _weights;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _weights = new EnchantmentWeightModifierService();
            _weights.Register();
            _inventory = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _inventory?.Dispose();
            _inventory = null;
            _weights?.Dispose();
            _weights = null;
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private ItemSO NewPassive(string id, float cursedWeightMult)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;
            item.CursedEnchantmentWeightMultiplier = cursedWeightMult;
            _spawned.Add(item);
            return item;
        }

        [Test]
        public void AddItem_RegistersCursedWeightMultiplier_WhenNotOne()
        {
            // Arrange + Act
            _inventory.AddItem(NewPassive("moneda.maldita", 3f));

            // Assert
            Assert.AreEqual(3f, _weights.ResolveCursedMultiplier(), 0.0001f);
        }

        [Test]
        public void RemoveItem_UnregistersCursedWeightMultiplier()
        {
            // Arrange
            _inventory.AddItem(NewPassive("moneda.maldita", 3f));

            // Act
            _inventory.RemoveItem("moneda.maldita");

            // Assert
            Assert.AreEqual(1f, _weights.ResolveCursedMultiplier(), 0.0001f);
        }

        [Test]
        public void AddItem_SkipsRegistration_WhenMultiplierIsOne()
        {
            // Arrange + Act — el default (1) no debe pasar por el servicio.
            _inventory.AddItem(NewPassive("item.neutro", 1f));

            // Assert
            Assert.AreEqual(1f, _weights.ResolveCursedMultiplier(), 0.0001f);
        }
    }
}
