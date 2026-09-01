using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Cobertura de <see cref="DamageBreakdownAnnouncer.AnnounceForceDoor"/>: emite el
    /// payload N×M con combo real (aunque el threshold vaya a fallar — ver el número es
    /// feedback), y no emite sin combo (no hay desglose que animar) ni con effect null.
    /// </summary>
    [TestFixture]
    public class DamageBreakdownAnnouncerForceDoorTests
    {
        private Guid _player;
        private bool _received;
        private DamageBreakdownComputedPayload _payload;
        private Action<DamageBreakdownComputedPayload> _handler;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = Guid.NewGuid();
            _received = false;
            _handler = p => { _received = true; _payload = p; };
            TypedEvent<DamageBreakdownComputedPayload>.Subscribe(_handler);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<DamageBreakdownComputedPayload>.Unsubscribe(_handler);
            ServiceLocator.Clear();
        }

        private EffectContext CtxWithCombo()
        {
            return new EffectContext
            {
                SourceGuid = _player,
                DiceResult = new[] { 4, 4, 4, 1, 1 },
                KeptDice = new[] { 4, 4, 4 },
                KeptDiceOriginalIndices = new[] { 0, 1, 2 },
                ComboResult = ComboDetectionResult.Match(
                    "combo.trio", baseDamage: 22, countUsed: 3,
                    contributingIndices: new[] { 0, 1, 2 }),
            };
        }

        [Test]
        public void AnnounceForceDoor_WithComboMatch_RaisesPayloadWithForceDoorKindAndNxMTotal()
        {
            // Arrange
            var eff = new EffForceDoor { RequiredValue = 99, ComboMultiplier = 1f };

            // Act — threshold 99 va a fallar, pero el desglose igual se anuncia.
            DamageBreakdownAnnouncer.AnnounceForceDoor(CtxWithCombo(), eff);

            // Assert — sin IDiceEnchantmentService el resolver no puede mapear caras
            // (ResolveFromContext → null), así que N = base 22 pelado; la aritmética
            // con Σcaras la cubren PlayerComboForceDoorTests / ActionRollServiceTests.
            // Acá importa el contrato del announcer: kind, comboId, sin target, y que
            // anuncia AUNQUE el check vaya a fallar.
            Assert.IsTrue(_received);
            Assert.AreEqual(PlayerComboFormulaKind.ForceDoor, _payload.Breakdown.Kind);
            Assert.AreEqual(22, _payload.Breakdown.Final);
            Assert.AreEqual("combo.trio", _payload.ComboId);
            Assert.AreEqual(Guid.Empty, _payload.TargetGuid);
        }

        [Test]
        public void AnnounceForceDoor_NoCombo_DoesNotRaise()
        {
            // Arrange — sin match no hay desglose que animar (el label plano cubre).
            var eff = new EffForceDoor();
            var ctx = new EffectContext
            {
                SourceGuid = _player,
                DiceResult = new[] { 3, 4, 5, 1, 2 },
                ComboResult = ComboDetectionResult.NoMatch(),
            };

            // Act
            DamageBreakdownAnnouncer.AnnounceForceDoor(ctx, eff);

            // Assert
            Assert.IsFalse(_received);
        }

        [Test]
        public void AnnounceForceDoor_NullEffect_NoOp()
        {
            // Act — mismo contrato defensivo que AnnounceHeal.
            DamageBreakdownAnnouncer.AnnounceForceDoor(CtxWithCombo(), null);

            // Assert
            Assert.IsFalse(_received);
        }
    }
}
