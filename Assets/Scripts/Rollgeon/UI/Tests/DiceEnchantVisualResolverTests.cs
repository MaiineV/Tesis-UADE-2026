using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre <see cref="DiceEnchantVisualResolver.ResolvePrimary"/> (prioridad de maldición y
    /// cupos vacíos: <c>GetEnchantments</c> puede traer nulls intercalados) y el query
    /// <see cref="EnchantmentCapabilityQueries.IsCursed"/> que lo alimenta.
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

        // Reflection porque _capabilities es protected y solo expone lectura: el runtime
        // nunca la muta, pero el test necesita poblarla como lo haría la autoría.
        private static void SetCapabilities(EnchantmentSO ench, params IEnchantmentCapability[] caps)
        {
            var field = typeof(EnchantmentSO).GetField("_capabilities",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "campo _capabilities no encontrado en EnchantmentSO");
            field.SetValue(ench, new List<IEnchantmentCapability>(caps));
        }

        private EnchantmentSO MakeCursedEnchantment(string name)
        {
            var ench = MakeEnchantment(name);
            SetCapabilities(ench, new CapCursed());
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

        /// <summary>
        /// El caso bendición + maldición en el mismo dado: la maldición gana el visual
        /// aunque venga después en la lista.
        /// </summary>
        [Test]
        public void ResolvePrimary_ReturnsCursed_WhenGoodEnchantmentComesFirst()
        {
            // Arrange
            var good = MakeEnchantment("Bendicion");
            var cursed = MakeCursedEnchantment("Maldicion");
            var enchantments = new[] { good, cursed };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(cursed, result);
        }

        [Test]
        public void ResolvePrimary_ReturnsCursed_WhenCursedIsBehindEmptySlots()
        {
            // Arrange
            var good = MakeEnchantment("Bendicion");
            var cursed = MakeCursedEnchantment("Maldicion");
            var enchantments = new[] { null, good, null, cursed };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(cursed, result);
        }

        [Test]
        public void ResolvePrimary_ReturnsFirstCursed_WhenSeveralAreCursed()
        {
            // Arrange
            var firstCursed = MakeCursedEnchantment("PrimeraMaldicion");
            var secondCursed = MakeCursedEnchantment("SegundaMaldicion");
            var enchantments = new[] { firstCursed, secondCursed };

            // Act
            var result = DiceEnchantVisualResolver.ResolvePrimary(enchantments);

            // Assert
            Assert.AreSame(firstCursed, result);
        }

        // ------------------------------------------------------------------
        // IsCursed
        // ------------------------------------------------------------------

        [Test]
        public void IsCursed_ReturnsFalse_WhenEnchantmentIsNull()
        {
            // Arrange + Act + Assert
            Assert.IsFalse(((EnchantmentSO)null).IsCursed());
        }

        [Test]
        public void IsCursed_ReturnsFalse_WhenCapabilitiesAreEmpty()
        {
            // Arrange
            var ench = MakeEnchantment("SinCaps");

            // Act + Assert
            Assert.IsFalse(ench.IsCursed());
        }

        [Test]
        public void IsCursed_ReturnsFalse_WhenOtherCapabilitiesArePresent()
        {
            // Arrange — capability real pero no maldita.
            var ench = MakeEnchantment("Lento");
            SetCapabilities(ench, new CapPreventHolding());

            // Act + Assert
            Assert.IsFalse(ench.IsCursed());
        }

        [Test]
        public void IsCursed_ReturnsTrue_WhenCapCursedIsPresent()
        {
            // Arrange
            var ench = MakeCursedEnchantment("Maldicion");

            // Act + Assert
            Assert.IsTrue(ench.IsCursed());
        }

        [Test]
        public void IsCursed_IgnoresNullCapabilityEntries()
        {
            // Arrange — la autoría puede dejar entradas null en la lista polimórfica.
            var ench = MakeEnchantment("MaldicionConHueco");
            SetCapabilities(ench, null, new CapCursed());

            // Act + Assert
            Assert.IsTrue(ench.IsCursed());
        }
    }
}