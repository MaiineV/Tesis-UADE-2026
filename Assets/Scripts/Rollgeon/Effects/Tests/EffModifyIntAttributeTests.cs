using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combos;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffModifyIntAttributeTests
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

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(50));
            attrs.SetAttribute<Energy>(new Energy(5));
            attrs.SetAttribute<Shield>(new Shield(0));
            attrs.SetAttribute<Attack>(new Attack(3));
            _attrManager.Register(_sourceId, attrs);

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

        // ── Operations × Constant source ───────────────────────────────

        [Test]
        public void Add_Constant_AddsToCurrent()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Add, 3);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(8, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void Subtract_Constant_SubtractsFromCurrent()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Subtract, 2);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(3, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void Multiply_Constant_MultipliesCurrent()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Multiply, 3);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(15, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void Divide_Constant_DividesCurrent()
        {
            _attrManager.SetAttributeValue<Energy, int>(_sourceId, 10);
            var eff = MakeConstant(StatType.Energy, IntOperation.Divide, 2);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void Divide_ByZero_NoOp_LogsWarning()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Divide, 0);
            LogAssert.Expect(LogType.Warning, new Regex(".*Divide-by-zero.*"));
            bool result = eff.ApplyEffect(MakeCtx());
            Assert.IsTrue(result, "Divide-by-zero no debe abortar la chain.");
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void Set_Constant_OverwritesCurrent()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Set, 99);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(99, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        // ── FromReader source ──────────────────────────────────────────

        [Test]
        public void FromReader_AddsReadValue()
        {
            var eff = MakeFromReader(StatType.Energy, IntOperation.Add,
                new ReadConstantInt { Value = 7 }, multiplier: 1f);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(12, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void FromReader_AppliesMultiplier()
        {
            var eff = MakeFromReader(StatType.Energy, IntOperation.Add,
                new ReadConstantInt { Value = 7 }, multiplier: 2f);
            eff.ApplyEffect(MakeCtx());
            // 5 + (7 * 2) = 19
            Assert.AreEqual(19, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void FromReader_NullReader_AmountIsZero()
        {
            var eff = MakeFromReader(StatType.Energy, IntOperation.Add, reader: null, multiplier: 1f);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        // ── ComboValue source ──────────────────────────────────────────

        [Test]
        public void ComboValue_Match_UsesBaseDamageWithMultiplier()
        {
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match(10, 2);
            var eff = MakeCombo(StatType.Energy, IntOperation.Add, comboMultiplier: 1.5f);
            eff.ApplyEffect(ctx);
            // 5 + RoundToInt(10 * 1.5) = 5 + 15 = 20
            Assert.AreEqual(20, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void ComboValue_NoMatch_AmountIsZero()
        {
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.NoMatch();
            var eff = MakeCombo(StatType.Energy, IntOperation.Add, comboMultiplier: 2f);
            eff.ApplyEffect(ctx);
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void ComboValue_NullComboResult_AmountIsZero()
        {
            var ctx = MakeCtx();
            ctx.ComboResult = null;
            var eff = MakeCombo(StatType.Energy, IntOperation.Add, comboMultiplier: 2f);
            eff.ApplyEffect(ctx);
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        // ── Target resolution ──────────────────────────────────────────

        [Test]
        public void EmptyTargetGuid_FallsBackToSourceGuid()
        {
            var ctx = MakeCtx();
            ctx.TargetGuid = Guid.Empty;
            var eff = MakeConstant(StatType.Energy, IntOperation.Add, 4);
            eff.ApplyEffect(ctx);
            Assert.AreEqual(9, _attrManager.GetAttributeValue<Energy, int>(_sourceId));
        }

        [Test]
        public void BothGuidsEmpty_ReturnsFalse_LogsWarning()
        {
            var ctx = MakeCtx();
            ctx.SourceGuid = Guid.Empty;
            ctx.TargetGuid = Guid.Empty;
            var eff = MakeConstant(StatType.Energy, IntOperation.Add, 4);
            LogAssert.Expect(LogType.Warning, new Regex(".*No target resolved.*"));
            bool result = eff.ApplyEffect(ctx);
            Assert.IsFalse(result);
        }

        // ── Stat routing ───────────────────────────────────────────────

        [Test]
        public void TargetStat_RoutesToCorrectAttribute()
        {
            // Health=50, Energy=5. Modificar Health debe NO tocar Energy.
            var eff = MakeConstant(StatType.Health, IntOperation.Add, 7);
            eff.ApplyEffect(MakeCtx());
            Assert.AreEqual(57, _attrManager.GetAttributeValue<Health, int>(_sourceId));
            Assert.AreEqual(5, _attrManager.GetAttributeValue<Energy, int>(_sourceId), "Energy no debe cambiar.");
        }

        // ── Pipeline contract ──────────────────────────────────────────

        [Test]
        public void NullContext_ReturnsFalse()
        {
            var eff = MakeConstant(StatType.Energy, IntOperation.Add, 5);
            Assert.IsFalse(eff.ApplyEffect(null));
        }

        [Test]
        public void NoAttributesManager_ReturnsFalse_LogsWarning()
        {
            ServiceLocator.Clear();
            var eff = MakeConstant(StatType.Energy, IntOperation.Add, 5);
            LogAssert.Expect(LogType.Warning, new Regex(".*AttributesManager not registered.*"));
            bool result = eff.ApplyEffect(MakeCtx());
            Assert.IsFalse(result);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static EffModifyIntAttribute MakeConstant(StatType stat, IntOperation op, int amount)
        {
            var eff = new EffModifyIntAttribute { TargetStat = stat, Operation = op };
            SetField(eff, "_amountSource", DamageSource.Constant);
            SetField(eff, "_baseAmount", amount);
            return eff;
        }

        private static EffModifyIntAttribute MakeFromReader(StatType stat, IntOperation op,
            EffectIntReader reader, float multiplier)
        {
            var eff = new EffModifyIntAttribute { TargetStat = stat, Operation = op };
            SetField(eff, "_amountSource", DamageSource.FromReader);
            SetField(eff, "_reader", reader);
            SetField(eff, "_readerMultiplier", multiplier);
            return eff;
        }

        private static EffModifyIntAttribute MakeCombo(StatType stat, IntOperation op, float comboMultiplier)
        {
            var eff = new EffModifyIntAttribute { TargetStat = stat, Operation = op };
            SetField(eff, "_amountSource", DamageSource.ComboValue);
            SetField(eff, "_comboMultiplier", comboMultiplier);
            return eff;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private EffectContext MakeCtx() => new EffectContext
        {
            SourceGuid = _sourceId,
            TargetGuid = Guid.Empty,
            lastResult = true,
        };
    }
}
