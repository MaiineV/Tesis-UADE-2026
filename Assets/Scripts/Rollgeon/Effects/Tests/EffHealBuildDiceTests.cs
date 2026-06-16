using NUnit.Framework;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Effects.Concretes;
using Rollgeon.Phase;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffHealBuildDiceTests
    {
        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        // Factor 1.0 (legacy / fórmula lineal). Spec base: HP = base + (score - threshold) × 1.
        [TestCase(5, 15, 14, 5)]
        [TestCase(5, 15, 16, 6)]
        [TestCase(5, 15, 17, 7)]
        [TestCase(5, 15, 15, 5)]   // sum == threshold → base + 0
        [TestCase(10, 15, 30, 25)] // bonus alto
        [TestCase(10, 15, 5, 10)]  // bien por debajo → base
        [TestCase(0, 15, 20, 5)]   // base 0 + 5 bonus → 5
        public void ComputeBuildDiceHeal_FactorOne_AppliesThresholdFormula(
            int baseAmount, int threshold, int score, int expected)
        {
            Assert.AreEqual(expected,
                EffHeal.ComputeBuildDiceHeal(baseAmount, threshold, score, scaleFactor: 1f, maxCap: 0));
        }

        // Tabla de la spec (valores de referencia por fase):
        // Early base 5, umbral 10, factor 0.8. Score 20 → 5 + floor(10 * 0.8) = 5 + 8 = 13.
        // Mid base 12, umbral 30, factor 0.3. Score 50 → 12 + floor(20 * 0.3) = 12 + 6 = 18.
        // Late base 20, umbral 80, factor 0.08. Score 200 → 20 + floor(120 * 0.08) = 20 + 9 = 29.
        // Endgame base 30, umbral 200, factor 0.02. Score 500 → 30 + floor(300 * 0.02) = 30 + 6 = 36.
        [TestCase(5, 10, 20, 0.8f, 0, 13, TestName = "Early phase reference")]
        [TestCase(12, 30, 50, 0.3f, 0, 18, TestName = "Mid phase reference")]
        [TestCase(20, 80, 200, 0.08f, 0, 29, TestName = "Late phase reference")]
        [TestCase(30, 200, 500, 0.02f, 0, 36, TestName = "Endgame phase reference")]
        public void ComputeBuildDiceHeal_FactorScalesBonus(
            int baseAmount, int threshold, int score, float factor, int cap, int expected)
        {
            Assert.AreEqual(expected,
                EffHeal.ComputeBuildDiceHeal(baseAmount, threshold, score, factor, cap));
        }

        // Floor abajo: el spec exige redondeo hacia abajo. Score=25, threshold=15, factor=0.7 →
        // 5 + floor(10 * 0.7) = 5 + floor(7.0) = 12. Score=26 → 5 + floor(11 * 0.7) = 5 + 7 = 12.
        // Score=27 → 5 + floor(12 * 0.7) = 5 + 8 = 13. Score=28 → 5 + floor(13 * 0.7) = 5 + 9 = 14.
        [TestCase(5, 15, 25, 0.7f, 12)]
        [TestCase(5, 15, 26, 0.7f, 12)]
        [TestCase(5, 15, 27, 0.7f, 13)]
        [TestCase(5, 15, 28, 0.7f, 14)]
        public void ComputeBuildDiceHeal_FloorsBonus(
            int baseAmount, int threshold, int score, float factor, int expected)
        {
            Assert.AreEqual(expected,
                EffHeal.ComputeBuildDiceHeal(baseAmount, threshold, score, factor, maxCap: 0));
        }

        [Test]
        public void ComputeBuildDiceHeal_CapClampsHigh()
        {
            // Sin cap, score 100 + base 5 + factor 1 + threshold 15 → 5 + 85 = 90.
            // Con cap 30, el resultado se clampea a 30.
            int withoutCap = EffHeal.ComputeBuildDiceHeal(5, 15, 100, 1f, maxCap: 0);
            int withCap = EffHeal.ComputeBuildDiceHeal(5, 15, 100, 1f, maxCap: 30);
            Assert.AreEqual(90, withoutCap);
            Assert.AreEqual(30, withCap);
        }

        [Test]
        public void ComputeBuildDiceHeal_CapZeroMeansNoCap()
        {
            // cap = 0 es la convención para "sin cap" — un heal alto pasa intacto.
            Assert.AreEqual(90, EffHeal.ComputeBuildDiceHeal(5, 15, 100, 1f, maxCap: 0));
        }

        [Test]
        public void ComputeBuildDiceHeal_CapDoesNotApplyBelowThreshold()
        {
            // Score bajo el umbral → heal = base. Si base < cap, el cap es inerte.
            Assert.AreEqual(5, EffHeal.ComputeBuildDiceHeal(5, 15, 10, 1f, maxCap: 100));
        }

        [Test]
        public void ComputeBuildDiceHeal_NegativeFactorClampsToZero()
        {
            // Defensa: un factor negativo no debe restar HP (no hay daño desde heal).
            // Sin escalado → solo base.
            Assert.AreEqual(5, EffHeal.ComputeBuildDiceHeal(5, 15, 100, -0.5f, maxCap: 0));
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOff_ReturnsFalse()
        {
            var heal = new EffHeal(); // _useBuildDice default false
            bool got = heal.TryGetRollSpec(System.Guid.Empty, out _);
            Assert.IsFalse(got);
        }

        [Test]
        public void BuildTooltip_BuildDiceOff_ReturnsNull()
        {
            // Sin _useBuildDice, no hay umbral/tope relevantes para mostrar.
            var heal = new EffHeal();
            Assert.IsNull(heal.BuildTooltip());
        }

        [Test]
        public void BuildTooltip_BuildDiceOn_ShowsBaseAndThreshold()
        {
            var heal = MakeHealWithBuildDice(energyCostInCombat: 2);
            SetPrivateField(heal, "_baseAmount", 5);
            SetPrivateField(heal, "_healThreshold", 15);

            var text = heal.BuildTooltip();
            Assert.IsNotNull(text);
            StringAssert.Contains("HP Base: 5", text);
            StringAssert.Contains("Umbral Mínimo: 15", text);
            // Sin cap configurado, no debe mencionar Tope.
            StringAssert.DoesNotContain("Tope Máximo", text);
        }

        [Test]
        public void BuildTooltip_BuildDiceOn_WithCap_ShowsTope()
        {
            var heal = MakeHealWithBuildDice(energyCostInCombat: 2);
            SetPrivateField(heal, "_baseAmount", 5);
            SetPrivateField(heal, "_healThreshold", 15);
            SetPrivateField(heal, "_healMaxCap", 40);

            var text = heal.BuildTooltip();
            Assert.IsNotNull(text);
            StringAssert.Contains("Tope Máximo: 40", text);
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_InCombat_ChargesCombatCost()
        {
            var heal = MakeHealWithBuildDice(energyCostInCombat: 2);
            RegisterPhase(GamePhase.Combat);

            bool got = heal.TryGetRollSpec(System.Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.IsTrue(spec.AlwaysSucceeds, "Heal no debe tratar 'sum < threshold' como fallo.");
            Assert.IsFalse(spec.RequireConfirm, "Heal va directo a roll, sin confirm dialog.");
            Assert.IsTrue(spec.AllowReroll);
            Assert.AreEqual(1, spec.RerollEnergyCost);
            Assert.AreEqual(2, spec.EnergyCost, "En combate Curarse debe cobrar _energyCostInCombat.");
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_OutOfCombat_CostIsZero()
        {
            var heal = MakeHealWithBuildDice(energyCostInCombat: 2);
            RegisterPhase(GamePhase.Exploration);

            bool got = heal.TryGetRollSpec(System.Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.AreEqual(0, spec.EnergyCost, "Fuera de combate la poción no debe gastar energía.");
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_NoPhaseService_DefaultsToOutOfCombat()
        {
            // Sin IPhaseService registrado → IsInCombat() = false → cost = 0.
            var heal = MakeHealWithBuildDice(energyCostInCombat: 99);

            bool got = heal.TryGetRollSpec(System.Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.AreEqual(0, spec.EnergyCost);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static EffHeal MakeHealWithBuildDice(int energyCostInCombat)
        {
            var heal = new EffHeal();
            SetPrivateField(heal, "_useBuildDice", true);
            SetPrivateField(heal, "_energyCostInCombat", energyCostInCombat);
            return heal;
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"Reflection layout cambió: '{name}' no encontrado.");
            field.SetValue(instance, value);
        }

        private static void RegisterPhase(GamePhase phase)
        {
            var fake = new FakePhaseServiceForHeal { CurrentBase = phase };
            ServiceLocator.AddService<IPhaseService>(fake, ServiceScope.Run);
        }

        private sealed class FakePhaseServiceForHeal : IPhaseService
        {
            public GamePhase CurrentBase { get; set; }
            public PhaseOverlay CurrentOverlay => PhaseOverlay.None;
            public void ReplacePhase(GamePhase next) => CurrentBase = next;
            public void PushOverlay(PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }
    }
}
