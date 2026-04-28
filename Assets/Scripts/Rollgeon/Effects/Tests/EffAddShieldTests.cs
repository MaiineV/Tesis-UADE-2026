using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combos;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffAddShieldTests
    {
        private AttributesManager _attrManager;
        private Guid _sourceId;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();

            var sourceAttrs = new ModifiableAttributes();
            sourceAttrs.EnsureInitialized();
            sourceAttrs.SetAttribute<Shield>(new Shield(0));
            _attrManager.Register(_sourceId, sourceAttrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ── Constant source ─────────────────────────────────────────────

        [Test]
        public void Constant_AddsShieldToTarget()
        {
            var eff = CreateConstantEffect(10);
            var ctx = MakeCtx();

            eff.ApplyEffect(ctx);

            Assert.AreEqual(10, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void Constant_AddsToExistingShield()
        {
            _attrManager.SetAttributeValue<Shield, int>(_sourceId, 5);
            var eff = CreateConstantEffect(10);
            var ctx = MakeCtx();

            eff.ApplyEffect(ctx);

            Assert.AreEqual(15, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void Constant_FiresOnShieldChangedEvent()
        {
            var eff = CreateConstantEffect(7);
            var ctx = MakeCtx();

            int capturedShield = -1;
            Guid capturedGuid = Guid.Empty;
            EventManager.Subscribe(EventName.OnShieldChanged, args =>
            {
                capturedGuid = (Guid)args[0];
                capturedShield = (int)args[1];
            });

            eff.ApplyEffect(ctx);

            Assert.AreEqual(_sourceId, capturedGuid);
            Assert.AreEqual(7, capturedShield);
        }

        [Test]
        public void Constant_StoresFloatingShieldOnBehavior()
        {
            var bh = new TestBehavior();
            var eff = CreateConstantEffect(12);
            var ctx = MakeCtx(bh);

            eff.ApplyEffect(ctx);

            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingShield, out var list));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(12f, list[0].Value);
            Assert.AreEqual(_sourceId, list[0].TargetEntityGuid);
        }

        [Test]
        public void ZeroAmount_ReturnsTrue_NoSideEffects()
        {
            var eff = CreateConstantEffect(0);
            var ctx = MakeCtx();

            bool result = eff.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void NullContext_ReturnsFalse()
        {
            var eff = CreateConstantEffect(5);

            bool result = eff.ApplyEffect(null);

            Assert.IsFalse(result);
        }

        [Test]
        public void NoSelection_FallsBackToSourceGuid()
        {
            var eff = CreateConstantEffect(20);
            var ctx = MakeCtx();
            ctx.SelectionResult = null;

            eff.ApplyEffect(ctx);

            Assert.AreEqual(20, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        // ── ComboValue source ───────────────────────────────────────────

        [Test]
        public void ComboValue_UsesComboBaseDamageTimesMultiplier()
        {
            var bh = new TestBehavior();
            var ctx = MakeCtx(bh);
            ctx.ComboResult = ComboDetectionResult.Match(20, 2);

            var eff = CreateComboEffect(1.5f);

            eff.ApplyEffect(ctx);

            // 20 * 1.5 = 30
            Assert.AreEqual(30, _attrManager.GetAttribute<Shield>(_sourceId).Value);
            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingShield, out var list));
            Assert.AreEqual(30f, list[0].Value);
        }

        [Test]
        public void ComboValue_FallsBackToBaseAmount_WhenNoCombo()
        {
            var eff = CreateComboEffect(2f, baseAmount: 8);
            var ctx = MakeCtx();
            ctx.ComboResult = null;

            eff.ApplyEffect(ctx);

            Assert.AreEqual(8, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_FallsBackToBaseAmount_WhenComboNoMatch()
        {
            var eff = CreateComboEffect(2f, baseAmount: 5);
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.NoMatch();

            eff.ApplyEffect(ctx);

            Assert.AreEqual(5, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_AddsToExistingShield()
        {
            _attrManager.SetAttributeValue<Shield, int>(_sourceId, 10);
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match(15, 2);

            var eff = CreateComboEffect(1f);

            eff.ApplyEffect(ctx);

            Assert.AreEqual(25, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private EffAddShield CreateConstantEffect(int amount)
        {
            var eff = new EffAddShield();
            SetField(eff, "_shieldSource", DamageSource.Constant);
            SetField(eff, "_baseAmount", amount);
            return eff;
        }

        private EffAddShield CreateComboEffect(float multiplier, int baseAmount = 0)
        {
            var eff = new EffAddShield();
            SetField(eff, "_shieldSource", DamageSource.ComboValue);
            SetField(eff, "_comboMultiplier", multiplier);
            SetField(eff, "_baseAmount", baseAmount);
            return eff;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private EffectContext MakeCtx(BaseBehavior bh = null)
        {
            return new EffectContext
            {
                SourceGuid = _sourceId,
                TargetGuid = Guid.Empty,
                lastResult = true,
                SourceBehavior = bh,
            };
        }
    }
}
