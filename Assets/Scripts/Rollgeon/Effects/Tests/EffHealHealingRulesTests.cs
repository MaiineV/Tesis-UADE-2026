using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Healing;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Concretes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Ayuno: <see cref="EffHeal"/> ignora las curas cuyo contexto viene de un item pasivo
    /// (<c>SourceItemId</c>) mientras <see cref="IHealingRuleService"/> las bloquee. Las curas
    /// sin item (clase, poción) siguen pasando.
    /// </summary>
    [TestFixture]
    public class EffHealHealingRulesTests
    {
        private const int MaxHp = 100;

        private AttributesManager _attrManager;
        private Guid _sourceId;
        private HealingRuleService _rules;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(50));
            _attrManager.Register(_sourceId, attrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
            ServiceLocator.AddService<IHealPipeline>(new HealPipeline(_attrManager, _ => MaxHp), ServiceScope.Run);
            _rules = new HealingRuleService();
            _rules.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _rules.Dispose();
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static EffHeal ConstantHeal(int amount)
        {
            var heal = new EffHeal();
            SetField(heal, "_baseAmount", amount);
            return heal;
        }

        private EffectContext Ctx(string sourceItemId) => new EffectContext
        {
            SourceGuid = _sourceId,
            SourceItemId = sourceItemId,
            lastResult = true,
        };

        private int Hp => _attrManager.GetAttribute<Health>(_sourceId).Value;

        [Test]
        public void PassiveItemHeal_RuleActive_IsIgnored_ButDoesNotCutTheChain()
        {
            _rules.AddPassiveHealingBlock("ayuno");
            LogAssert.Expect(LogType.Log,
                "[EffHeal] Cura de 'talisman.vital' ignorada — curación de items pasivos bloqueada (Ayuno).");

            bool ok = ConstantHeal(10).ApplyEffect(Ctx("talisman.vital"));

            Assert.IsTrue(ok);
            Assert.AreEqual(50, Hp);
        }

        [Test]
        public void PassiveItemHeal_RuleInactive_Heals()
        {
            ConstantHeal(10).ApplyEffect(Ctx("talisman.vital"));

            Assert.AreEqual(60, Hp);
        }

        [Test]
        public void HealWithoutItemSource_RuleActive_StillHeals()
        {
            // Cura de clase / poción: SourceItemId null → Ayuno no la toca.
            _rules.AddPassiveHealingBlock("ayuno");

            ConstantHeal(10).ApplyEffect(Ctx(null));

            Assert.AreEqual(60, Hp);
        }

        [Test]
        public void PassiveItemHeal_WithoutRuleService_Heals()
        {
            _rules.Dispose();
            ServiceLocator.Clear();
            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
            ServiceLocator.AddService<IHealPipeline>(new HealPipeline(_attrManager, _ => MaxHp), ServiceScope.Run);
            _rules = new HealingRuleService(); // para el TearDown

            ConstantHeal(10).ApplyEffect(Ctx("talisman.vital"));

            Assert.AreEqual(60, Hp);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"campo privado '{name}' no encontrado en {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
