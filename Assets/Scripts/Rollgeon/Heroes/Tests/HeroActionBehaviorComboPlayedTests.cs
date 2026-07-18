using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Effects;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Tests del emisor de <c>ComboPlayedPayload</c> en <see cref="HeroActionBehavior.Execute"/>:
    /// una emisión por ejecución con combo, ventana abierta durante los efectos y cerrada al salir.
    /// </summary>
    [TestFixture]
    public class HeroActionBehaviorComboPlayedTests
    {
        private ComboPlayService _play;
        private int _raiseCount;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _play = new ComboPlayService();
            _play.Register();
            _raiseCount = 0;
            TypedEvent<ComboPlayedPayload>.Subscribe(OnComboPlayed);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<ComboPlayedPayload>.Clear();
            _play.Dispose();
            ServiceLocator.Clear();
        }

        private void OnComboPlayed(ComboPlayedPayload payload) => _raiseCount++;

        private static HeroBehaviorContext BuildComboContext()
        {
            return new HeroBehaviorContext
            {
                DiceResult = new[] { 2, 2, 5 },
                KeptDice = new[] { 2, 2 },
                KeptDiceOriginalIndices = new[] { 0, 1 },
                MatchedComboResult = ComboDetectionResult.Match("combo.par", baseDamage: 15,
                    countUsed: 2, contributingIndices: new[] { 0, 1 }),
                TargetGuid = Guid.NewGuid(),
            };
        }

        private static HeroActionBehavior BuildBehavior(params IEffect[] effects)
        {
            var group = new EffectData();
            foreach (var eff in effects) group.Effects.Add(eff);
            return new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData> { group },
            };
        }

        [Test]
        public void Execute_WithMatchedCombo_RaisesComboPlayedOnce()
        {
            // Arrange
            var behavior = BuildBehavior(new WindowProbeEffect(_play));

            // Act
            behavior.Execute(BuildComboContext());

            // Assert
            Assert.AreEqual(1, _raiseCount);
        }

        [Test]
        public void Execute_NoCombo_DoesNotRaise()
        {
            // Arrange
            var behavior = BuildBehavior(new WindowProbeEffect(_play));

            // Act
            behavior.Execute(new HeroBehaviorContext { DiceResult = new[] { 1, 3, 5 } });

            // Assert
            Assert.AreEqual(0, _raiseCount);
        }

        [Test]
        public void Execute_WindowOpenDuringEffects_AndClosedAfter()
        {
            // Arrange — el suscriptor inyecta bono al scratch en el Raise; el efecto
            // observa la ventana (y ese bono) durante su Apply.
            TypedEvent<ComboPlayedPayload>.Subscribe(_ => _play.CurrentPlayScratch.BonusComboDamage += 5);
            var probe = new WindowProbeEffect(_play);
            var behavior = BuildBehavior(probe);

            // Act
            behavior.Execute(BuildComboContext());

            // Assert
            Assert.IsTrue(probe.WindowWasOpenDuringApply);
            Assert.AreEqual(5, probe.BonusSeenDuringApply);
            Assert.IsFalse(_play.IsPlayWindowOpen);
            Assert.IsNull(_play.CurrentPlayScratch);
        }

        [Test]
        public void Execute_EffectThrows_StillClosesWindow()
        {
            // Arrange
            var behavior = BuildBehavior(new ThrowingEffect());

            // Act
            Assert.Throws<InvalidOperationException>(() => behavior.Execute(BuildComboContext()));

            // Assert — el try/finally del emisor cierra la ventana igual.
            Assert.IsFalse(_play.IsPlayWindowOpen);
            Assert.IsNull(_play.CurrentPlayScratch);
        }

        [Test]
        public void Execute_PassiveBonus_ReachesDamageFormulaDuringWindow()
        {
            // Arrange — un suscriptor de ComboPlayed (una pasiva) inyecta +5 al play
            // scratch; un efecto del behavior resuelve la fórmula DENTRO de la ventana,
            // como hace EffDealDamage en el flujo real.
            TypedEvent<ComboPlayedPayload>.Subscribe(_ => _play.CurrentPlayScratch.BonusComboDamage += 5);
            var resolver = new ResolveDamageProbeEffect();
            var behavior = BuildBehavior(resolver);

            // Act
            behavior.Execute(BuildComboContext());

            // Assert — (15 base combo del contexto) + 5 de bono_combo del canal at-played.
            Assert.AreEqual(20, resolver.ResolvedDamage);
            // Con la ventana cerrada, la fórmula vuelve al valor sin bono (el preview no lo ve).
            Assert.AreEqual(10, Rollgeon.Combat.Damage.PlayerComboDamage.Resolve(
                Guid.NewGuid(), 10, null));
        }

        [Test]
        public void Execute_WithoutPlayService_RunsWithoutError()
        {
            // Arrange
            _play.Dispose();
            ServiceLocator.Clear();
            var probe = new WindowProbeEffect(_play);
            var behavior = BuildBehavior(probe);

            // Act / Assert — sin service registrado, el emisor es no-op.
            Assert.DoesNotThrow(() => behavior.Execute(BuildComboContext()));
            Assert.AreEqual(1, probe.ApplyCount);
        }

        private sealed class WindowProbeEffect : IEffect
        {
            private readonly IComboPlayService _service;
            public int ApplyCount;
            public bool WindowWasOpenDuringApply;
            public int BonusSeenDuringApply;

            public WindowProbeEffect(IComboPlayService service) { _service = service; }

            public string GetEffectName() => "WindowProbe";
            public Effects.Selection.SelectionSettings GetSelection() => new Effects.Selection.SelectionSettings();
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context)
            {
                ApplyCount++;
                WindowWasOpenDuringApply = _service.IsPlayWindowOpen && _service.CurrentPlayScratch != null;
                BonusSeenDuringApply = _service.CurrentPlayScratch?.BonusComboDamage ?? 0;
                return true;
            }
        }

        private sealed class ResolveDamageProbeEffect : IEffect
        {
            public int ResolvedDamage;

            public string GetEffectName() => "ResolveDamageProbe";
            public Effects.Selection.SelectionSettings GetSelection() => new Effects.Selection.SelectionSettings();
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context)
            {
                ResolvedDamage = Rollgeon.Combat.Damage.PlayerComboDamage.Resolve(
                    context.SourceGuid, context.ComboResult?.BaseDamage ?? 0, null);
                return true;
            }
        }

        private sealed class ThrowingEffect : IEffect
        {
            public string GetEffectName() => "Throwing";
            public Effects.Selection.SelectionSettings GetSelection() => new Effects.Selection.SelectionSettings();
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context) => throw new InvalidOperationException("boom");
        }
    }
}
