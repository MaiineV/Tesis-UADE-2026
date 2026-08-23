using System.Reflection;
using NUnit.Framework;
using Rollgeon.Effects.Concretes;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="DamageFormulaView.ResolveHealPreviewArgs"/>: el preview N×M de
    /// Curarse (Spec Heal N×M) tiene que combinar la base de la HealBaseTable del sheet
    /// (ver <c>ContractSheetHealBaseTests.GetHealBase_*</c>) con la perilla de habilidad
    /// del EffHeal — los mismos dos números que arma la rama de escudo espejo
    /// (ResolvePlayerShieldBase + shieldEff.ComboMultiplier en DamageFormulaView).
    /// <c>DamageFormulaView</c> es un MonoBehaviour sin seam para testear la rama entera
    /// sin escena — este helper es la parte pura extraída para no dejar la combinación
    /// base+multiplier sin cobertura.
    /// </summary>
    [TestFixture]
    public class DamageFormulaViewHealPreviewTests
    {
        [Test]
        public void ResolveHealPreviewArgs_ComboMultiplierDefault_ReturnsSheetBaseWithMultiplierOne()
        {
            // Arrange
            var healEffect = CreateHealEffect(comboMultiplier: 1f);

            // Act
            var args = DamageFormulaView.ResolveHealPreviewArgs(healBaseFromSheet: 8, healEffect);

            // Assert
            Assert.AreEqual(8, args.Base);
            Assert.AreEqual(1f, args.Multiplier);
        }

        [Test]
        public void ResolveHealPreviewArgs_ComboMultiplierConfigured_ReadsMultiplierFromEffect()
        {
            // Arrange — la perilla por habilidad (EffHeal._comboMultiplier) tiene que pasar
            // intacta al breakdown, igual que shieldEff.ComboMultiplier en la rama de escudo.
            var healEffect = CreateHealEffect(comboMultiplier: 2.5f);

            // Act
            var args = DamageFormulaView.ResolveHealPreviewArgs(healBaseFromSheet: 8, healEffect);

            // Assert
            Assert.AreEqual(8, args.Base);
            Assert.AreEqual(2.5f, args.Multiplier);
        }

        [Test]
        public void ResolveHealPreviewArgs_MissingTableEntry_PreviewsZeroBase()
        {
            // Arrange — combo sin entrada en la HealBaseTable: ContractSheet.GetHealBase
            // devuelve 0 (gate explícito, ver ContractSheetHealBaseTests), el preview
            // tiene que reflejar ese 0 tal cual, no un fallback distinto al heal real.
            var healEffect = CreateHealEffect(comboMultiplier: 1f);

            // Act
            var args = DamageFormulaView.ResolveHealPreviewArgs(healBaseFromSheet: 0, healEffect);

            // Assert
            Assert.AreEqual(0, args.Base);
        }

        [Test]
        public void ResolveHealPreviewArgs_NullEffect_DefaultsMultiplierToOne()
        {
            // Arrange / Act — no debería pasar en runtime (el llamador en DamageFormulaView
            // ya gatea por healEff != null antes de invocar), pero la combinación no debe
            // reventar si algún día se llama sin effect resuelto.
            var args = DamageFormulaView.ResolveHealPreviewArgs(healBaseFromSheet: 8, healEffect: null);

            // Assert
            Assert.AreEqual(8, args.Base);
            Assert.AreEqual(1f, args.Multiplier);
        }

        // Mismo patrón que EffHealBuildDiceTests.CreateBuildDiceEffect: EffHeal serializa
        // sus campos vía Odin/UnityEngine, no expone setters públicos — reflection es el
        // único seam sin pasar por el inspector.
        private static EffHeal CreateHealEffect(float comboMultiplier)
        {
            var heal = new EffHeal();
            SetField(heal, "_comboMultiplier", comboMultiplier);
            return heal;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }
    }
}
