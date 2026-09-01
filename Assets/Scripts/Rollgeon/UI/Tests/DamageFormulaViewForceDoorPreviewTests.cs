using NUnit.Framework;
using Rollgeon.ActionRolls;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="DamageFormulaView.ResolveForceDoorPreviewArgs"/>: el preview
    /// N×M de Forzar Puerta combina la base layered del combo (ComboMatchedPayload) con
    /// el multiplicador del spec — normalizado, porque un spec default llega con 0.
    /// Mismo criterio de extracción pura que <see cref="DamageFormulaViewHealPreviewTests"/>.
    /// </summary>
    [TestFixture]
    public class DamageFormulaViewForceDoorPreviewTests
    {
        [Test]
        public void ResolveForceDoorPreviewArgs_UsesSpecMultiplier_WhenSet()
        {
            // Arrange
            var spec = new ActionRollSpec { ComboMultiplier = 1.5f };

            // Act
            var args = DamageFormulaView.ResolveForceDoorPreviewArgs(comboFlatBase: 22, in spec);

            // Assert
            Assert.AreEqual(22, args.Base);
            Assert.AreEqual(1.5f, args.Multiplier);
        }

        [Test]
        public void ResolveForceDoorPreviewArgs_DefaultsMultiplierToOne_WhenUnset()
        {
            // Arrange — struct default: ComboMultiplier 0 debe normalizar a 1, nunca
            // multiplicar el preview a 0.
            var spec = new ActionRollSpec();

            // Act
            var args = DamageFormulaView.ResolveForceDoorPreviewArgs(comboFlatBase: 22, in spec);

            // Assert
            Assert.AreEqual(22, args.Base);
            Assert.AreEqual(1f, args.Multiplier);
        }
    }
}
