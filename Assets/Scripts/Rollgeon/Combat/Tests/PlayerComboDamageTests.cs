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
    /// Tests de <see cref="PlayerComboDamage.Resolve"/> (fórmula v3, N×M exacto):
    /// N = comboBase + ATQ + bonos + Σcaras + bono_combo; M = scratch × ability.
    /// </summary>
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

        private static ContributingDie[] DiceOf(params (DiceType type, int face)[] spec)
        {
            var result = new ContributingDie[spec.Length];
            for (int i = 0; i < spec.Length; i++)
                result[i] = new ContributingDie(i, spec[i].face, spec[i].type);
            return result;
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
        public void Resolve_GoldenRuleV3_EverythingAdditive_ScaledByMultipliers()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = DiceOf((DiceType.D20, 15));

            int result = PlayerComboDamage.Resolve(_player, comboBaseDamage: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            // N = 10 + 5 + 3 + 15 = 33; M = 2 → 66
            Assert.AreEqual(66, result);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesWholeN_IncludingPlayerBase()
        {
            RegisterPlayerAttack(5);

            // N = 10 + 5 = 15; M = 2 → 30 (en v2 el ATQ quedaba fuera del producto)
            Assert.AreEqual(30, PlayerComboDamage.Resolve(_player, 10, null, abilityMultiplier: 2f));
        }

        [Test]
        public void Resolve_FacesSum_AddsRolledFacesToN()
        {
            var dice = DiceOf((DiceType.D6, 4), (DiceType.D20, 18));

            // N = 10 + 4 + 18 = 32
            Assert.AreEqual(32, PlayerComboDamage.Resolve(_player, 10, dice));
        }

        [Test]
        public void Resolve_FacesSum_DiceTypeIrrelevant_OnlyFacesCount()
        {
            // El multiplicador por tipo (EV/3.5) murió en v3: misma cara ⇒ mismo daño.
            int withD6 = PlayerComboDamage.Resolve(_player, 10, DiceOf((DiceType.D6, 5)));
            int withD20 = PlayerComboDamage.Resolve(_player, 10, DiceOf((DiceType.D20, 5)));

            Assert.AreEqual(15, withD6);
            Assert.AreEqual(withD6, withD20);
        }

        [Test]
        public void Resolve_NoContributingDice_NoFaceTerm()
        {
            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, 10, Array.Empty<ContributingDie>()));
        }

        [Test]
        public void Resolve_ScratchBonus_EntersN_AndIsMultiplied()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            // N = 10 + 4 = 14; M = 2 → 28 (en v2 el bono iba después del multi: 24)
            Assert.AreEqual(28, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_BlockComboDamage_ReturnsZero()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 99, DiceOf((DiceType.D20, 20)), abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_PlayScratchBonus_EntersN_AndIsMultiplied()
        {
            var play = new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            // N = 10 + 4 = 14; M = 2 → 28
            Assert.AreEqual(28, PlayerComboDamage.Resolve(_player, 10, null));
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

            // N = 10 + 4 + 3 = 17; M = 2 × 1.5 = 3 → 51
            Assert.AreEqual(51, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_PlayScratchBlock_ReturnsZero()
        {
            var play = new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 99, DiceOf((DiceType.D20, 20)), abilityMultiplier: 5f));
        }

        [Test]
        public void Resolve_PlayWindowClosed_DoesNotAffectResult()
        {
            var play = new FakeComboPlayService { Scratch = null };
            ServiceLocator.AddService<IComboPlayService>(play, ServiceScope.Global);

            Assert.AreEqual(10, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void RoundNxM_ExactHalf_RoundsAwayFromZero()
        {
            // Mathf.RoundToInt haría banker's rounding (6.5 → 6): la regla v3 es 6.5 → 7.
            Assert.AreEqual(7, PlayerComboDamage.RoundNxM(13, 0.5f));
        }

        [Test]
        public void RoundNxM_RegularFraction_RoundsToNearest()
        {
            // 13 × 0.75 = 9.75 → 10
            Assert.AreEqual(10, PlayerComboDamage.RoundNxM(13, 0.75f));
        }

        [Test]
        public void RoundNxM_NegativeProduct_ClampsToZero()
        {
            Assert.AreEqual(0, PlayerComboDamage.RoundNxM(10, -1f));
        }

        // Fake mínimo: solo LastComboScratch importa para la fórmula.
        // ---- Canal aditivo sobre M: M = (1 + Σbonus) × Πmult × ability ----------------

        [Test]
        public void Resolve_MultiplierBonus_AddsToOneBeforeScaling()
        {
            // Piedra Angular: +2 sobre el 1 de M → ×3 sin otros factores.
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 2f }
            }, ServiceScope.Global);

            Assert.AreEqual(30, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_MultiplierBonus_ComposesAdditivelyAcrossChannels_ThenMultiplies()
        {
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 2f, ComboDamageMultiplier = 2f }
            }, ServiceScope.Global);
            ServiceLocator.AddService<IComboPlayService>(new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 1f, ComboDamageMultiplier = 1.5f }
            }, ServiceScope.Global);

            // M = (1 + 2 + 1) × (2 × 1.5) = 12 → 120
            Assert.AreEqual(120, PlayerComboDamage.Resolve(_player, 10, null));
        }

        [Test]
        public void Resolve_MultiplierBonus_ScalesWithAbilityMultiplier()
        {
            ServiceLocator.AddService<IComboPlayService>(new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 1f }
            }, ServiceScope.Global);

            // M = (1 + 1) × 0.75 = 1.5 → 15
            Assert.AreEqual(15, PlayerComboDamage.Resolve(_player, 10, null, abilityMultiplier: 0.75f));
        }

        [Test]
        public void Resolve_MultiplierBonusNegative_ClampsAtZero()
        {
            // Sin clamp propio: (1 − 2) deja M negativo y RoundNxM corta en 0.
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = -2f }
            }, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 10, null));
        }

        private sealed class FakeComboPassiveService : IComboPassiveService
        {
            public EnchantmentScratch Scratch;
            public bool IsReady => true;
            public IReadOnlyList<ComboPassiveSO> GetPassivesFor(string comboId) => Array.Empty<ComboPassiveSO>();
            public void Apply(ComboPassiveSO passive) { }
            public int GetBonusDamage(string comboId) => 0;
            public EnchantmentScratch LastComboScratch => Scratch;
        }

        // Fake mínimo del canal at-played: la fórmula lee LastPlayScratch (persiste para el
        // daño diferido). CurrentPlayScratch refleja el mismo scratch durante la ventana.
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
