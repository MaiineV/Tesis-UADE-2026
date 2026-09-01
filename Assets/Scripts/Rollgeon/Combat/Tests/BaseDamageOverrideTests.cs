using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Effects.Readers;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// El override de daño base (Furia Contenida / Egoísta) reemplaza SOLO
    /// <c>dmg_base_PJ</c> de la fórmula N×M — los bonos de Attack de otros items
    /// (<c>bonos_PJ</c>) quedan intactos.
    /// </summary>
    public sealed class BaseDamageOverrideTests
    {
        BaseDamageOverrideService _service;
        AttributesManager _attrMgr;
        Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _player = Guid.NewGuid();

            _attrMgr = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrMgr);
            var attrs = new ModifiableAttributes();
            var attack = new Attack(5); // dmg_base_PJ = 5
            attrs.SetAttribute<Attack>(attack);
            _attrMgr.Register(_player, attrs);
            // +3 de "otro item" (bonos_PJ): tiene que sobrevivir al override.
            _attrMgr.AddModifier<Attack, int>(_player, new Modifier<int>(3,
                ModifierOperation.Add, 0, _player, Guid.NewGuid(),
                ModifierDirection.Intrinsic, ModifierLifetime.Permanent, default));

            _service = new BaseDamageOverrideService();
            _service.Register(); // se auto-registra en el locator
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _attrMgr?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        int ResolveDamage(out DamageBreakdown breakdown)
            => PlayerComboDamage.Resolve(_player, comboBaseDamage: 10, contributingDice: null,
                abilityMultiplier: 1f, PlayerComboFormulaKind.Damage, out breakdown);

        [Test]
        public void WithoutOverride_FormulaUsesAttackRaw()
        {
            var total = ResolveDamage(out var b);

            Assert.AreEqual(5, b.AttackBase);
            Assert.AreEqual(3, b.AttackBonus);
            Assert.AreEqual(18, total, "10 combo + 5 base + 3 bonus");
        }

        [Test]
        public void Override_ReplacesOnlyTheBaseTerm()
        {
            _service.Register("item.furia.test", new ReadConstantInt { Value = 0 }, priority: 0);

            var total = ResolveDamage(out var b);

            Assert.AreEqual(0, b.AttackBase, "Furia: el daño base pasa a lo que diga el reader");
            Assert.AreEqual(3, b.AttackBonus, "los +Attack de otros items NO se pisan");
            Assert.AreEqual(13, total, "10 combo + 0 base + 3 bonus");
        }

        [Test]
        public void Unregister_RestoresTheRawBase()
        {
            _service.Register("item.furia.test", new ReadConstantInt { Value = 0 }, priority: 0);
            _service.Unregister("item.furia.test");

            ResolveDamage(out var b);

            Assert.AreEqual(5, b.AttackBase);
        }

        [Test]
        public void TwoOverrides_HigherPriorityWins_AndWarns()
        {
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("BaseDamageOverrideService.*2 overrides"));
            _service.Register("item.a", new ReadConstantInt { Value = 7 }, priority: 0);
            _service.Register("item.b", new ReadConstantInt { Value = 2 }, priority: 10);

            ResolveDamage(out var b);

            Assert.AreEqual(2, b.AttackBase, "gana el priority más alto");
        }

        [Test]
        public void NegativeReaderValue_ClampsToZero()
        {
            _service.Register("item.x", new ReadConstantInt { Value = -4 }, priority: 0);

            Assert.IsTrue(_service.TryGetBaseDamage(_player, out var value));
            Assert.AreEqual(0, value, "un daño base negativo no existe");
        }
    }
}
