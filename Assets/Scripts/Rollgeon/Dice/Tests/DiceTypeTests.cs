using NUnit.Framework;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Tabla literal de TECHNICAL.md §6.1. Si una entrada falla, es porque alguien
    /// cambió el spec sin actualizar el código (o vice-versa).
    /// </summary>
    [TestFixture]
    public class DiceTypeTests
    {
        [TestCase(DiceType.D3, 3)]
        [TestCase(DiceType.D4, 4)]
        [TestCase(DiceType.D6, 6)]
        [TestCase(DiceType.D8, 8)]
        [TestCase(DiceType.D10, 10)]
        [TestCase(DiceType.D12, 12)]
        [TestCase(DiceType.D20, 20)]
        public void MaxFace_MatchesSpec(DiceType type, int expected)
        {
            Assert.AreEqual(expected, type.MaxFace());
        }

        [TestCase(DiceType.D3, 5)]
        [TestCase(DiceType.D4, 5)]
        [TestCase(DiceType.D6, 5)]
        [TestCase(DiceType.D8, 4)]
        [TestCase(DiceType.D10, 3)]
        [TestCase(DiceType.D12, 2)]
        [TestCase(DiceType.D20, 1)]
        public void MaxPerBag_MatchesSpec(DiceType type, int expected)
        {
            Assert.AreEqual(expected, type.MaxPerBag());
        }

        // Sala de Encantamiento — GDD: D3/D4=1, D6/D8=2, D10/D12=3, D20=4.
        [TestCase(DiceType.D3, 1)]
        [TestCase(DiceType.D4, 1)]
        [TestCase(DiceType.D6, 2)]
        [TestCase(DiceType.D8, 2)]
        [TestCase(DiceType.D10, 3)]
        [TestCase(DiceType.D12, 3)]
        [TestCase(DiceType.D20, 4)]
        public void MaxEnchantmentSlots_MatchesSpec(DiceType type, int expected)
        {
            Assert.AreEqual(expected, type.MaxEnchantmentSlots());
        }

        // Tabla literal del Spec de Daño v2 (Santi, 2026-07-06): EV = (MaxFace+1)/2.
        // El d6 (3.5) es la línea base ×1.00 de multi_dmg_combo.
        [TestCase(DiceType.D3, 2.0f)]
        [TestCase(DiceType.D4, 2.5f)]
        [TestCase(DiceType.D6, 3.5f)]
        [TestCase(DiceType.D8, 4.5f)]
        [TestCase(DiceType.D10, 5.5f)]
        [TestCase(DiceType.D12, 6.5f)]
        [TestCase(DiceType.D20, 10.5f)]
        public void ExpectedValue_MatchesDamageSpec(DiceType type, float expected)
        {
            Assert.AreEqual(expected, type.ExpectedValue(), 0.0001f);
        }

        [Test]
        public void BaselineExpectedValue_MatchesD6()
        {
            Assert.AreEqual(DiceType.D6.ExpectedValue(), DiceTypeExt.BaselineExpectedValue, 0.0001f);
        }
    }
}
