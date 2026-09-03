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
    /// Tests de <see cref="PlayerComboShield.Resolve"/>: fórmula compartida con el daño
    /// (v3, N×M exacto — N = shieldBase + ATQ + bonos + Σcaras + bono_combo; M = scratch ×
    /// ability) con una única divergencia: el gate de base 0 (sin entrada en la
    /// ShieldBaseTable no hay escudo, ni siquiera el término de Attack).
    /// </summary>
    [TestFixture]
    public class PlayerComboShieldTests
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
        public void Resolve_NoAttackNoDice_ReturnsShieldBaseOnly()
        {
            Assert.AreEqual(10, PlayerComboShield.Resolve(_player, shieldBase: 10, contributingDice: null));
        }

        [Test]
        public void Resolve_AddsPlayerBaseAttack_WhenNoModifiers()
        {
            RegisterPlayerAttack(5);

            Assert.AreEqual(15, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_GoldenRuleV3_EverythingAdditive_ScaledByMultipliers()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = DiceOf((DiceType.D20, 15));

            int result = PlayerComboShield.Resolve(_player, shieldBase: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            // N = 10 + 5 + 3 + 15 = 33; M = 2 → 66
            Assert.AreEqual(66, result);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesWholeN_IncludingPlayerBase()
        {
            RegisterPlayerAttack(5);

            // N = 10 + 5 = 15; M = 2 → 30
            Assert.AreEqual(30, PlayerComboShield.Resolve(_player, 10, null, abilityMultiplier: 2f));
        }

        [Test]
        public void Resolve_FacesSum_AddsRolledFaces_Uncapped()
        {
            var dice = DiceOf((DiceType.D20, 18), (DiceType.D20, 7));

            // N = 10 + 18 + 7 = 35 — pasa entero, sin cap.
            Assert.AreEqual(35, PlayerComboShield.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_FacesSum_MixedDiceTypes_OnlyFacesCount()
        {
            var dice = DiceOf((DiceType.D6, 3), (DiceType.D20, 20));

            // N = 10 + 3 + 20 = 33
            Assert.AreEqual(33, PlayerComboShield.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_PassiveScratch_BonusEntersN_AndIsMultiplied()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            // N = 10 + 4 = 14; M = 2 → 28
            Assert.AreEqual(28, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_BlockComboDamage_BlocksShieldToo()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboShield.Resolve(_player, 99, DiceOf((DiceType.D20, 20)), abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_PlayAndMatchScratches_ComposeAcrossChannels()
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
            Assert.AreEqual(51, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_ShieldBaseZero_ReturnsZero_EvenWithAttackRegistered()
        {
            // El gate es la única divergencia con el daño: sin entrada en la ShieldBaseTable
            // (fallback 0) el combo NO genera escudo — ni siquiera el término de Attack.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);

            Assert.AreEqual(0, PlayerComboShield.Resolve(_player, 0, DiceOf((DiceType.D20, 20))));
        }

        [Test]
        public void Resolve_ParityWithDamageFormula_ForSameInputs()
        {
            // Anti-drift estructural: con base > 0, escudo y daño son LA MISMA fórmula.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 2);
            var passives = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch
                {
                    BonusComboDamage = 4, ComboDamageMultiplier = 1.5f, ComboMultiplierBonus = 0.5f,
                }
            };
            ServiceLocator.AddService<IComboPassiveService>(passives, ServiceScope.Global);
            var dice = DiceOf((DiceType.D6, 4), (DiceType.D20, 12));

            int shield = PlayerComboShield.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);
            int damage = PlayerComboDamage.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);

            // N = 7 + 5 + 2 + 16 + 4 = 34; M = (1 + 0.5) × 1.5 × 0.75 = 1.6875 → 57.375 → 57
            Assert.AreEqual(57, shield);
            Assert.AreEqual(damage, shield);
        }

        [Test]
        public void Resolve_MultiplierBonus_AppliesToShield()
        {
            // Ayuno "+3 a todos los multiplicadores" también escala el escudo (fórmula compartida).
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 2f }
            }, ServiceScope.Global);

            Assert.AreEqual(30, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_LargeBase_PassesThroughUncapped()
        {
            // Sin cap (BUG-021 se contiene con el reset de escudo por turno, no con un tope):
            // la base grande y las caras pasan enteras.
            var dice = DiceOf((DiceType.D20, 20), (DiceType.D20, 20), (DiceType.D20, 20));

            int shield = PlayerComboShield.Resolve(_player, 90, dice);

            // N = 90 + 60 = 150; M = 1 → 150
            Assert.AreEqual(150, shield);
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

        // Fake mínimo del canal at-played (mismo criterio que PlayerComboDamageTests).
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
