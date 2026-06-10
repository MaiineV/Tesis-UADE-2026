using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="PlayerComboDamage.Resolve"/> — la fórmula unificada
    /// (dañoBasePJ + bonosPJ + (comboBase + bonosCombo)) × multiplicador. Cubre: daño base del PJ
    /// (stat Attack), el multiplicador escalando todo, los bonos de scratch y el multiplicador de
    /// scratch (antes nunca consumido), y el block de daño.
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

        private void RegisterPlayerAttack(int attack)
        {
            var a = new ModifiableAttributes();
            a.SetAttribute<Attack>(new Attack(attack));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);
        }

        [Test]
        public void Resolve_NoPlayerAttackNoScratch_ReturnsComboBaseTimesMultiplier()
        {
            Assert.AreEqual(20, PlayerComboDamage.Resolve(_player, comboBaseDamage: 10, comboMultiplier: 2f));
        }

        [Test]
        public void Resolve_AddsPlayerAttackBaseDamage()
        {
            RegisterPlayerAttack(5);

            Assert.AreEqual(15, PlayerComboDamage.Resolve(_player, 10, 1f));
        }

        [Test]
        public void Resolve_MultiplierScalesPlayerBaseAndCombo()
        {
            RegisterPlayerAttack(5);

            // (5 + 10) * 2 = 30
            Assert.AreEqual(30, PlayerComboDamage.Resolve(_player, 10, 2f));
        }

        [Test]
        public void Resolve_AddsScratchBonus_AndAppliesScratchMultiplier()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            // (0 + 10 + 4) * 1 * 2 = 28
            Assert.AreEqual(28, PlayerComboDamage.Resolve(_player, 10, 1f));
        }

        [Test]
        public void Resolve_BlockComboDamage_ReturnsZero()
        {
            var fake = new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BlockComboDamage = true }
            };
            ServiceLocator.AddService<IComboPassiveService>(fake, ServiceScope.Global);

            Assert.AreEqual(0, PlayerComboDamage.Resolve(_player, 99, 5f));
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
