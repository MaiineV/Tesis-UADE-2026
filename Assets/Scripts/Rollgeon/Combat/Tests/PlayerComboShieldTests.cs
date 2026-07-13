using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="PlayerComboShield.Resolve"/> (Spec Escudo v2):
    /// <c>min(escudo_combo_base × multi_dmg_combo, ShieldCap)</c>. Fórmula pura —
    /// sin Attack, sin bono_combo, sin scratch.
    /// </summary>
    [TestFixture]
    public class PlayerComboShieldTests
    {
        [Test]
        public void Resolve_AllD6_ReturnsBaseTimesNeutralMultiplier()
        {
            // Arrange
            var dice = new List<DiceType> { DiceType.D6, DiceType.D6, DiceType.D6 };

            // Act — multi = 3.5/3.5 = 1.0
            int shield = PlayerComboShield.Resolve(shieldBase: 4, dice);

            // Assert
            Assert.AreEqual(4, shield);
        }

        [Test]
        public void Resolve_NoContributingDice_UsesNeutralMultiplier()
        {
            // Arrange / Act — sin dados el multi es 1.0 (mismo contrato que el daño)
            int shield = PlayerComboShield.Resolve(shieldBase: 5, contributingDice: null);

            // Assert
            Assert.AreEqual(5, shield);
        }

        [Test]
        public void Resolve_MixedD6D20_AveragesExpectedValue()
        {
            // Arrange — EV avg = (3.5 + 10.5) / 2 = 7.0 → multi = 2.0
            var dice = new List<DiceType> { DiceType.D6, DiceType.D20 };

            // Act
            int shield = PlayerComboShield.Resolve(shieldBase: 3, dice);

            // Assert — 3 × 2.0 = 6, bajo el cap
            Assert.AreEqual(6, shield);
        }

        [Test]
        public void Resolve_AllD20_CapsAtShieldCap()
        {
            // Arrange — multi = 10.5/3.5 = 3.0
            var dice = new List<DiceType> { DiceType.D20, DiceType.D20 };

            // Act — 4 × 3.0 = 12 > cap
            int shield = PlayerComboShield.Resolve(shieldBase: 4, dice);

            // Assert
            Assert.AreEqual(PlayerComboShield.ShieldCap, shield);
        }

        [Test]
        public void Resolve_CapAppliesAfterMultiplication_NotBefore()
        {
            // Arrange — si el cap se aplicara antes de multiplicar, min(3,8)=3 → ×3 = 9.
            var dice = new List<DiceType> { DiceType.D20 };

            // Act — orden correcto: 3 × 3.0 = 9 → min(9, 8) = 8
            int shield = PlayerComboShield.Resolve(shieldBase: 3, dice);

            // Assert
            Assert.AreEqual(8, shield);
        }

        [Test]
        public void Resolve_ZeroBase_ReturnsZero()
        {
            // Arrange
            var dice = new List<DiceType> { DiceType.D20 };

            // Act — sin entrada en la tabla de escudo (fallback 0) no hay escudo
            int shield = PlayerComboShield.Resolve(shieldBase: 0, dice);

            // Assert
            Assert.AreEqual(0, shield);
        }

        [Test]
        public void Resolve_AttackTableScaleBase_NeverExceedsCap()
        {
            // Regression BUG-021: con la fórmula vieja una Generala (BaseDamage 90 de la
            // tabla de ATAQUE) daba 90 de escudo ≈ 45 turnos de inmunidad. Aún si un valor
            // de esa escala llegara a la fórmula nueva, el cap lo corta.
            // Arrange
            var dice = new List<DiceType> { DiceType.D20, DiceType.D20, DiceType.D20 };

            // Act
            int shield = PlayerComboShield.Resolve(shieldBase: 90, dice);

            // Assert
            Assert.AreEqual(PlayerComboShield.ShieldCap, shield);
        }
    }
}
