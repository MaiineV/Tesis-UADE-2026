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
    /// <summary>Tests de <see cref="PlayerComboDamage.Resolve"/>.</summary>
    [TestFixture]
    public class PlayerComboDamageTests
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

        [Test]
        public void Resolve_NoAttackNoDice_ReturnsComboBaseOnly()
        {
            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, comboBaseDamage: 10, contributingDice: null));
        }

        [Test]
        public void Resolve_AddsPlayerBaseAttack_WhenNoModifiers()
        {
            RegisterPlayerAttack(5);

            Assert.AreEqual(15, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_GoldenRule_BaseAndBonusPJ_NeverMultiplied()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = new[] { DiceType.D20 };

            int result = PlayerComboDamage.Resolve(_player, comboBaseDamage: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            // (5 + 3) + (10 × 3.0 × 2) = 68
            Assert.AreEqual(68, result);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesComboTermOnly_NotPlayerBase()
        {
            RegisterPlayerAttack(5);

            // 5 + (10 × 1 × 2) = 25
            Assert.AreEqual(25, PlayerComboDamage.Resolve(_player, 10, null, abilityMultiplier: 2f));
        }

        [Test]
        public void Resolve_MultiDmgCombo_AllD6_IsBaselineOne()
        {
            var dice = new[] { DiceType.D6, DiceType.D6 };
            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_MultiDmgCombo_AllD20_TriplesComboTerm()
        {
            var dice = new[] { DiceType.D20, DiceType.D20 };
            // 10 × (10.5/3.5) = 10 × 3.0 = 30
            Assert.AreEqual(30, PlayerComboDamage.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_MultiDmgCombo_MixedDice_AveragesExpectedValue()
        {
            var dice = new[] { DiceType.D6, DiceType.D20 };
            // EV avg = (3.5 + 10.5) / 2 = 7.0 → 7.0/3.5 = 2.0 → 10 × 2.0 = 20
            Assert.AreEqual(20, PlayerComboDamage.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_NoContributingDice_DefaultsToNeutralMultiplier()
        {
            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, 10, Array.Empty<DiceType>()));
        }

        [Test]
        public void Resolve_BonoCombo_AddedAfterMultiplier_NotBefore()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            // (10 × 2) + 4 = 24
            Assert.AreEqual(24, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_BlockComboDamage_ReturnsZero()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 99, new[] { DiceType.D20 }, abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_PlayScratchBonus_AddedAfterMultiplier()
        {
            var play = new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            // (10 × 2) + 4 = 24
            Assert.AreEqual(24, PlayerComboDamage.Resolve(_player, 10, null));
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
            Assert.AreEqual(37, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_PlayScratchBlock_ReturnsZero()
        {
            var play = new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 99, new[] { DiceType.D20 }, abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_PlayWindowClosed_DoesNotAffectResult()
        {
            var play = new FakeComboPlayService { Scratch = null };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, 10, null));
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

        // Fake mínimo del canal at-played: solo CurrentPlayScratch importa para la fórmula.
        private sealed class FakeComboPlayService : IComboPlayService
        {
            public EnchantmentScratch Scratch;
            public EnchantmentScratch CurrentPlayScratch => Scratch;
            public bool IsPlayWindowOpen => Scratch != null;
            public string CurrentComboId => null;
            public void BeginPlay(EffectContext effCtx) { }
            public void EndPlay() { }
        }
    }
}
