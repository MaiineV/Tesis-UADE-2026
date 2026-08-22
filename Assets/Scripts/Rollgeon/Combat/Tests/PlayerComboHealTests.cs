using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="PlayerComboHeal.Resolve"/>: fórmula compartida con el daño
    /// (v3, N×M exacto — N = healBase + ATQ + bonos + Σcaras + bono_combo; M = scratch ×
    /// ability) con la misma divergencia que el escudo: el gate de base 0 (sin entrada
    /// en la HealBaseTable no hay curación, ni siquiera el término de Attack).
    /// Espejo de <see cref="PlayerComboShieldTests"/>.
    /// </summary>
    [TestFixture]
    public class PlayerComboHealTests
    {
        private AttributesManager _attrs;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            _attrs?.Dispose();
        }

        private void RegisterPlayerAttack(int baseValue)
        {
            var a = new ModifiableAttributes();
            a.SetAttribute<Attack>(new Attack(baseValue));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);
        }

        private void RegisterPlayerAttackWithFlatBonus(int baseValue, int flatBonus)
        {
            RegisterPlayerAttack(baseValue);
            var mod = new Modifier<int>(
                amount: flatBonus, op: ModifierOperation.Add, duration: 0,
                carrierId: _player, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            _attrs.AddModifier<Attack, int>(_player, mod);
        }

        private static ContributingDie[] DiceOf(params (DiceType type, int face)[] spec)
        {
            var result = new ContributingDie[spec.Length];
            for (int i = 0; i < spec.Length; i++)
                result[i] = new ContributingDie(i, spec[i].face, spec[i].type);
            return result;
        }

        [Test]
        public void Resolve_NoAttackNoDice_ReturnsHealBaseOnly()
        {
            Assert.AreEqual(10, PlayerComboHeal.Resolve(_player, healBase: 10, contributingDice: null));
        }

        [Test]
        public void Resolve_AddsPlayerBaseAttack_WhenNoModifiers()
        {
            RegisterPlayerAttack(5);

            Assert.AreEqual(15, PlayerComboHeal.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_GoldenRuleV3_EverythingAdditive_ScaledByMultipliers()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = DiceOf((DiceType.D20, 15));

            int result = PlayerComboHeal.Resolve(_player, healBase: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            // N = 10 + 5 + 3 + 15 = 33; M = 2 → 66
            Assert.AreEqual(66, result);
        }

        [Test]
        public void Resolve_BlockComboDamage_BlocksHealToo()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboHeal.Resolve(_player, 99, DiceOf((DiceType.D20, 20)), abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_ScratchChannels_BonusEntersN_AndIsMultiplied()
        {
            var passives = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            var play = new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 3, ComboDamageMultiplier = 1.5f }
            };
            ServiceLocator.AddService<IComboPassiveService>(passives, ServiceScope.Global);
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            // N = 10 + 4 + 3 = 17; M = 3 → 51
            Assert.AreEqual(51, PlayerComboHeal.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_HealBaseZero_ReturnsZero_EvenWithAttackRegistered()
        {
            // El gate es la misma divergencia que el escudo: sin entrada en la HealBaseTable
            // (fallback 0) el combo NO cura — ni siquiera el término de Attack.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);

            Assert.AreEqual(0, PlayerComboHeal.Resolve(_player, 0, DiceOf((DiceType.D20, 20))));
        }

        [Test]
        public void Resolve_HealBaseZero_BreakdownCarriesHealKind()
        {
            PlayerComboHeal.Resolve(_player, 0, null, 1f, out var breakdown);

            Assert.AreEqual(PlayerComboFormulaKind.Heal, breakdown.Kind);
            Assert.AreEqual(0, breakdown.Final);
        }

        [Test]
        public void Resolve_ParityWithDamageFormula_ForSameInputs()
        {
            // Anti-drift estructural: con base > 0, curación y daño son LA MISMA fórmula.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 2);
            var passives = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 1.5f }
            };
            ServiceLocator.AddService<IComboPassiveService>(passives, ServiceScope.Global);
            var dice = DiceOf((DiceType.D6, 4), (DiceType.D20, 12));

            int heal = PlayerComboHeal.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);
            int damage = PlayerComboDamage.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);

            // N = 7 + 5 + 2 + 16 + 4 = 34; M = 1.5 × 0.75 = 1.125 → 38.25 → 38
            Assert.AreEqual(38, heal);
            Assert.AreEqual(damage, heal);
        }

        [Test]
        public void Resolve_WithBase_BreakdownCarriesHealKind()
        {
            PlayerComboHeal.Resolve(_player, 10, null, 1f, out var breakdown);

            Assert.AreEqual(PlayerComboFormulaKind.Heal, breakdown.Kind);
            Assert.AreEqual(10, breakdown.Final);
        }

        // Fake mínimo: solo LastComboScratch importa para la fórmula.
        private sealed class FakeComboPassiveService : IComboPassiveService
        {
            public EnchantmentScratch Scratch;
            public bool IsReady => true;
            public IReadOnlyList<ComboPassiveSO> GetPassivesFor(string comboId) => Array.Empty<ComboPassiveSO>();
            public void Apply(ComboPassiveSO passive) { }
            public int GetBonusDamage(string comboId) => 0;
            public EnchantmentScratch LastComboScratch => Scratch;
        }

        // Fake mínimo del canal at-played (mismo criterio que PlayerComboShieldTests).
        private sealed class FakeComboPlayService : IComboPlayService
        {
            public EnchantmentScratch Scratch;
            public EnchantmentScratch CurrentPlayScratch => Scratch;
            public EnchantmentScratch LastPlayScratch => Scratch;
            public bool IsPlayWindowOpen => Scratch != null;
            public string CurrentComboId => null;
            public void BeginPlay(EffectContext effCtx) { }
            public void EndPlay() { }
        }
    }
}
