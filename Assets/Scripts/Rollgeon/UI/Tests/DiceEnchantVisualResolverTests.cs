using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre <see cref="DiceEnchantVisualResolver.ResolvePrimary"/>, sobre todo el caso de los
    /// cupos vacíos: <c>GetEnchantments</c> puede traer nulls intercalados.
    /// </summary>
    [TestFixture]
    public class DiceEnchantVisualResolverTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private EnchantmentSO MakeEnchantment(string name)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = name;
            _created.Add(ench);
            return ench;
        }

        [Test]
        public void ResolvePrimary_ReturnsNull_WhenListIsNull()
        {
            // Arrange + Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(null);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ResolvePrimary_ReturnsNull_WhenListIsEmpty()
        {
            // Arrange
            var enchantments = new EnchantmentSO[0];

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ResolvePrimary_ReturnsNull_WhenEverySlotIsEmpty()
        {
            // Arrange — un dado con cupos pero sin encantar.
            var enchantments = new EnchantmentSO[] { null, null, null };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ResolvePrimary_ReturnsOnlyEnchantment()
        {
            // Arrange
            var ench = MakeEnchantment("Solo");
            var enchantments = new[] { ench };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(ench, result);
        }

        /// <summary>
        /// El bug que shippearía un <c>list[0]</c> ingenuo: el dado está encantado en el segundo
        /// cupo y el primero está vacío.
        /// </summary>
        [Test]
        public void ResolvePrimary_SkipsEmptySlots_AndReturnsFirstRealEnchantment()
        {
            // Arrange
            var ench = MakeEnchantment("EnSegundoCupo");
            var enchantments = new[] { null, ench };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(ench, result);
        }

        [Test]
        public void ResolvePrimary_ReturnsFirstReal_WhenSeveralAreEnchanted()
        {
            // Arrange
            var first = MakeEnchantment("Primero");
            var second = MakeEnchantment("Segundo");
            var enchantments = new[] { null, first, second };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(first, result);
        }
    }
}