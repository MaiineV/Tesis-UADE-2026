using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cobertura pura (sin GameObjects) de <see cref="FloatingNumberFormat"/> — mapeo
    /// dato → texto/tint/escala/motion — y de los easings de
    /// <see cref="FloatingDamageInstance"/>. Plan feedback-numerico-combate §3/§6.
    /// </summary>
    [TestFixture]
    public class FloatingNumberFormatTests
    {
        // ── ForDamage ────────────────────────────────────────────────────

        [Test]
        public void ForDamage_Outgoing_UsesDealtTintAndPlainText()
        {
            var style = FloatingNumberFormat.ForDamage(10, incoming: false);

            Assert.AreEqual("10", style.Text);
            Assert.AreEqual(FloatingNumberPalette.DamageDealt, style.Tint);
            Assert.AreEqual(1f, style.Scale);
            Assert.AreEqual(FloatingMotion.Rise, style.Motion);
        }

        [Test]
        public void ForDamage_Incoming_UsesMinusPrefixAndTakenTint()
        {
            var style = FloatingNumberFormat.ForDamage(10, incoming: true);

            Assert.AreEqual("-10", style.Text);
            Assert.AreEqual(FloatingNumberPalette.DamageTaken, style.Tint);
            Assert.AreEqual(1.1f, style.Scale);
        }

        [Test]
        public void ForDamage_OutgoingWeakness_UsesBangSuffixAndWeaknessTint()
        {
            var style = FloatingNumberFormat.ForDamage(10, incoming: false, weakness: true);

            Assert.AreEqual("10!", style.Text);
            Assert.AreEqual(FloatingNumberPalette.DamageWeakness, style.Tint);
            Assert.AreEqual(1.25f, style.Scale);
        }

        [Test]
        public void ForDamage_IncomingWinsOverWeakness()
        {
            // Un golpe recibido siempre se pinta incoming — el flag de weakness es
            // irrelevante en ese caso (no tiene sentido "debilidad" del lado receptor).
            var style = FloatingNumberFormat.ForDamage(5, incoming: true, weakness: true);

            Assert.AreEqual("-5", style.Text);
            Assert.AreEqual(FloatingNumberPalette.DamageTaken, style.Tint);
        }

        // ── ForType ──────────────────────────────────────────────────────

        [Test]
        public void ForType_Heal_PlusPrefixGreenTint()
        {
            var style = FloatingNumberFormat.ForType(FloatingNumberType.Heal, 7.4f);

            Assert.AreEqual("+7", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Heal, style.Tint);
            Assert.AreEqual(0.95f, style.Scale);
            Assert.AreEqual(FloatingMotion.Rise, style.Motion);
        }

        [Test]
        public void ForType_Shield_PlusPrefixCyanTint()
        {
            var style = FloatingNumberFormat.ForType(FloatingNumberType.Shield, 7f);

            Assert.AreEqual("+7", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Shield, style.Tint);
            Assert.AreEqual(0.95f, style.Scale);
        }

        [Test]
        public void ForType_Status_PlusPrefixVioletTint()
        {
            var style = FloatingNumberFormat.ForType(FloatingNumberType.Status, 7f);

            Assert.AreEqual("+7", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Status, style.Tint);
            Assert.AreEqual(0.9f, style.Scale);
        }

        [Test]
        public void ForType_Gold_PlusSuffixGoldTintAndArcMotion()
        {
            var style = FloatingNumberFormat.ForType(FloatingNumberType.Gold, 7f);

            Assert.AreEqual("+7 G", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Gold, style.Tint);
            Assert.AreEqual(0.9f, style.Scale);
            Assert.AreEqual(FloatingMotion.Arc, style.Motion);
        }

        [Test]
        public void ForType_Damage_DelegatesToForDamageOutgoing()
        {
            var style = FloatingNumberFormat.ForType(FloatingNumberType.Damage, 5f);
            var expected = FloatingNumberFormat.ForDamage(5, incoming: false);

            Assert.AreEqual(expected.Text, style.Text);
            Assert.AreEqual(expected.Tint, style.Tint);
            Assert.AreEqual(expected.Scale, style.Scale);
        }

        // ── Contraste visual: daño saliente vs oro ──────────────────────

        [Test]
        public void DamageDealtTint_And_GoldTint_AreVisuallyDistinct()
        {
            var dealt = FloatingNumberPalette.DamageDealt;
            var gold = FloatingNumberPalette.Gold;

            float maxChannelDelta = Mathf.Max(
                Mathf.Abs(dealt.r - gold.r),
                Mathf.Abs(dealt.g - gold.g),
                Mathf.Abs(dealt.b - gold.b));

            Assert.Greater(maxChannelDelta, 0.15f,
                "El daño saliente y el oro deben distinguirse a simple vista (regresión: " +
                "antes ambos eran ~amarillo dorado).");
        }

        // ── ShieldBlocked / ShieldBroken ─────────────────────────────────

        [Test]
        public void ShieldBlocked_ReturnsBloqueadoText()
        {
            var style = FloatingNumberFormat.ShieldBlocked();

            Assert.AreEqual("Bloqueado", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Shield, style.Tint);
        }

        [Test]
        public void ShieldBroken_ReturnsEscudoRotoText()
        {
            var style = FloatingNumberFormat.ShieldBroken();

            Assert.AreEqual("Escudo roto", style.Text);
            Assert.AreEqual(FloatingNumberPalette.Shield, style.Tint);
        }

        // ── Easings (FloatingDamageInstance) ────────────────────────────

        [Test]
        public void EaseOutCubic_BoundaryValues()
        {
            Assert.AreEqual(0f, FloatingDamageInstance.EaseOutCubic(0f), 1e-5f);
            Assert.AreEqual(1f, FloatingDamageInstance.EaseOutCubic(1f), 1e-5f);
        }

        [Test]
        public void EaseOutCubic_IsMonotonicallyIncreasing()
        {
            float previous = FloatingDamageInstance.EaseOutCubic(0f);
            for (float t = 0.1f; t <= 1f; t += 0.1f)
            {
                float current = FloatingDamageInstance.EaseOutCubic(t);
                Assert.GreaterOrEqual(current, previous,
                    $"EaseOutCubic no debe decrecer (t={t}).");
                previous = current;
            }
        }

        [Test]
        public void EaseOutBack_BoundaryValues()
        {
            Assert.AreEqual(0f, FloatingDamageInstance.EaseOutBack(0f), 1e-4f);
            Assert.AreEqual(1f, FloatingDamageInstance.EaseOutBack(1f), 1e-4f);
        }

        [Test]
        public void EaseOutBack_OvershootsPastOneNearPointSeven()
        {
            Assert.Greater(FloatingDamageInstance.EaseOutBack(0.7f), 1f,
                "El overshoot natural del back-ease debe superar 1.0 alrededor de t≈0.7.");
        }

        [Test]
        public void EaseInQuad_BoundaryValues()
        {
            Assert.AreEqual(0f, FloatingDamageInstance.EaseInQuad(0f), 1e-5f);
            Assert.AreEqual(1f, FloatingDamageInstance.EaseInQuad(1f), 1e-5f);
        }
    }
}
