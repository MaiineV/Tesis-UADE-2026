using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Concretes;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffLowHpAttackBuffTests
    {
        private AttributesManager _attrManager;
        private Guid _entityId;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _entityId = Guid.NewGuid();

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            attrs.SetAttribute<Attack>(new Attack(5));
            _attrManager.Register(_entityId, attrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void HpAtOrBelowThreshold_AddsAttackBonus()
        {
            _attrManager.SetAttributeValue<Health, int>(_entityId, 3);

            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            eff.ApplyEffect(MakeCtx());

            Assert.AreEqual(10, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId));
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Attack, int>(_entityId), "Raw Value no cambia, solo ModifiedValue.");
        }

        [Test]
        public void HpHealedAboveThreshold_RemovesAttackBonus()
        {
            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);

            _attrManager.SetAttributeValue<Health, int>(_entityId, 2);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(10, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId));

            _attrManager.SetAttributeValue<Health, int>(_entityId, 4);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(5, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId));
        }

        [Test]
        public void RepeatedCallsWhileBuffed_DoesNotStackModifier()
        {
            _attrManager.SetAttributeValue<Health, int>(_entityId, 1);

            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            eff.ApplyEffect(MakeCtx());
            eff.ApplyEffect(MakeCtx());
            eff.ApplyEffect(MakeCtx());

            Assert.AreEqual(10, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId),
                "Llamadas repetidas con el mismo estado no deben duplicar el modifier.");
        }

        [Test]
        public void RepeatedCallsWhileHealthy_StaysUnbuffed()
        {
            _attrManager.SetAttributeValue<Health, int>(_entityId, 10);

            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            eff.ApplyEffect(MakeCtx());
            eff.ApplyEffect(MakeCtx());

            Assert.AreEqual(5, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId));
        }

        [Test]
        public void HpAtZero_DoesNotApplyBuff()
        {
            _attrManager.SetAttributeValue<Health, int>(_entityId, 0);

            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            eff.ApplyEffect(MakeCtx());

            Assert.AreEqual(5, _attrManager.GetAttributeModifiedValue<Attack, int>(_entityId),
                "Una entidad con 0 HP no debe recibir el bonus de ataque.");
        }

        [Test]
        public void NullContext_ReturnsFalse()
        {
            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            Assert.IsFalse(eff.ApplyEffect(null));
        }

        [Test]
        public void NoAttributesManager_ReturnsFalse()
        {
            ServiceLocator.Clear();
            var eff = MakeEffect(hpThreshold: 3, attackBonus: 5);
            Assert.IsFalse(eff.ApplyEffect(MakeCtx()));
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static EffLowHpAttackBuff MakeEffect(int hpThreshold, int attackBonus)
        {
            var eff = new EffLowHpAttackBuff();
            SetField(eff, "_hpThreshold", hpThreshold);
            SetField(eff, "_attackBonus", attackBonus);
            return eff;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private EffectContext MakeCtx() => new EffectContext
        {
            SourceGuid = _entityId,
            TargetGuid = Guid.Empty,
            lastResult = true,
        };
    }
}
