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
    /// Tests de <see cref="PlayerComboShield.Resolve"/> (Spec Escudo v3): fórmula compartida
    /// con el daño — Attack + bonos + (shieldBase × multi × ability × scratch) + bono_combo —
    /// con una única divergencia: el gate de base 0 (sin entrada en la ShieldBaseTable no
    /// hay escudo, ni siquiera el término de Attack).
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

        // Caras arbitrarias: la aritmética vigente solo pondera por Type. Cuando la fórmula
        // pase a sumar caras (v3), los tests fijan caras explícitas.
        private static ContributingDie[] DiceOf(params DiceType[] types)
        {
            var result = new ContributingDie[types.Length];
            for (int i = 0; i < types.Length; i++) result[i] = new ContributingDie(i, 1, types[i]);
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
        public void Resolve_GoldenRule_BaseAndBonusPJ_NeverMultiplied()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = DiceOf(DiceType.D20);

            int result = PlayerComboShield.Resolve(_player, shieldBase: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            // (5 + 3) + (10 × 3.0 × 2) = 68
            Assert.AreEqual(68, result);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesShieldTermOnly_NotPlayerBase()
        {
            RegisterPlayerAttack(5);

            // 5 + (10 × 1 × 2) = 25
            Assert.AreEqual(25, PlayerComboShield.Resolve(_player, 10, null, abilityMultiplier: 2f));
        }

        [Test]
        public void Resolve_MultiDmgCombo_AllD20_TriplesShieldTerm_Uncapped()
        {
            // Con la fórmula v2 esto capeaba en 8; ahora pasa entero.
            var dice = DiceOf(DiceType.D20, DiceType.D20);
            // 10 × (10.5/3.5) = 30
            Assert.AreEqual(30, PlayerComboShield.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_MultiDmgCombo_MixedDice_AveragesExpectedValue()
        {
            var dice = DiceOf(DiceType.D6, DiceType.D20);
            // EV avg = (3.5 + 10.5) / 2 = 7.0 → 7.0/3.5 = 2.0 → 10 × 2.0 = 20
            Assert.AreEqual(20, PlayerComboShield.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_PassiveScratch_BonusAddedAfterMultiplier()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            // (10 × 2) + 4 = 24
            Assert.AreEqual(24, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_BlockComboDamage_BlocksShieldToo()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboShield.Resolve(_player, 99, DiceOf(DiceType.D20), abilityMultiplier: 5f));
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

            // (10 × 2 × 1.5) + 4 + 3 = 37
            Assert.AreEqual(37, PlayerComboShield.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_ShieldBaseZero_ReturnsZero_EvenWithAttackRegistered()
        {
            // El gate es la única divergencia con el daño: sin entrada en la ShieldBaseTable
            // (fallback 0) el combo NO genera escudo — ni siquiera el término de Attack.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);

            Assert.AreEqual(0, PlayerComboShield.Resolve(_player, 0, DiceOf(DiceType.D20)));
        }

        [Test]
        public void Resolve_ParityWithDamageFormula_ForSameInputs()
        {
            // Anti-drift estructural: con base > 0, escudo y daño son LA MISMA fórmula.
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 2);
            var passives = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 1.5f }
            };
            ServiceLocator.AddService<IComboPassiveService>(passives, ServiceScope.Global);
            var dice = DiceOf(DiceType.D6, DiceType.D20);

            int shield = PlayerComboShield.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);
            int damage = PlayerComboDamage.Resolve(_player, 7, dice, abilityMultiplier: 0.75f);

            Assert.AreEqual(damage, shield);
        }

        [Test]
        public void Resolve_LargeBase_PassesThroughUncapped()
        {
            // Con la Spec v2, BUG-021 se contenía con el cap (90 → 8). En v3 no hay cap:
            // el freno anti-inmunidad es el reset de escudo por turno + la escala ×10 del
            // daño enemigo.
            var dice = DiceOf(DiceType.D20, DiceType.D20, DiceType.D20);

            int shield = PlayerComboShield.Resolve(_player, 90, dice);

            // 90 × 3.0 = 270
            Assert.AreEqual(270, shield);
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
