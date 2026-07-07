using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="PlayerComboDamage.Resolve"/> — fórmula v2 (Spec de Daño, Santi
    /// 2026-07-06): <c>dmg_base_PJ + bonos_PJ + (comboBase × multi_dmg_combo) + bono_combo</c>.
    /// Cubre: separación aditivo/multiplicativo (regla de oro), cálculo de multi_dmg_combo
    /// desde EV de los dados contribuyentes, el multiplicador por habilidad y el de scratch
    /// (Gemelo/Par-Impar) escalando solo el término de combo, y el block de daño.
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

        // ---- dmg_base_PJ + bonos_PJ: aditivo puro, nunca multiplicado --------

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

        /// <summary>
        /// La regla de oro: dmg_base_PJ (5) y bonos_PJ (3, de un modifier Intrinsic) quedan
        /// FUERA del multiplicador de habilidad/dados — solo el término de combo escala.
        /// (5 + 3) + (10 × 3.0 [EV d20] × 2 [ability]) = 8 + 60 = 68.
        /// </summary>
        [Test]
        public void Resolve_GoldenRule_BaseAndBonusPJ_NeverMultiplied()
        {
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            var dice = new[] { DiceType.D20 };

            int result = PlayerComboDamage.Resolve(_player, comboBaseDamage: 10,
                contributingDice: dice, abilityMultiplier: 2f);

            Assert.AreEqual(68, result);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesComboTermOnly_NotPlayerBase()
        {
            RegisterPlayerAttack(5);

            // multi_dmg_combo = 1.0 (sin dice info) → 5 + (10 × 1 × 2) = 25, NO (5+10)×2=30.
            Assert.AreEqual(25, PlayerComboDamage.Resolve(_player, 10, null, abilityMultiplier: 2f));
        }

        // ---- multi_dmg_combo: EV de los dados contribuyentes / 3.5 -----------

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

        // ---- bono_combo: aditivo, se suma DESPUÉS del multiplicador ----------

        /// <summary>
        /// La otra mitad de la regla de oro: bono_combo (4, de una pasiva de combo) NO se
        /// multiplica junto con daño_combo_base. scratchMultiplier (2, ej. Gemelo) solo
        /// escala el término de combo: (10 × 2) + 4 = 24, NO (10 + 4) × 2 = 28.
        /// </summary>
        [Test]
        public void Resolve_BonoCombo_AddedAfterMultiplier_NotBefore()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

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
    }
}
