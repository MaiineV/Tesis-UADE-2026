using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combos;
using Rollgeon.Effects.Concretes;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Integration-level check for "combo repetido = 0 daño": exercises the same
    /// call sequence as <c>CombatHandoffService.DoConfirm</c> — <c>ComboLogService.Record</c>
    /// followed by <c>EffDealDamage.ApplyEffect</c> going through the real
    /// <see cref="DamagePipeline"/> — instead of poking <c>DamagePipeline.Resolve</c>
    /// directly like <c>DamagePipelineTests</c> does. Confirms the wiring end-to-end,
    /// not just the guard's internal algorithm.
    /// </summary>
    [TestFixture]
    public class EffDealDamage_RepeatComboTests
    {
        private AttributesManager _attrManager;
        private ComboLogService _comboLog;
        private Guid _sourceId;
        private Guid _targetId;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();
            _targetId = Guid.NewGuid();

            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(1000));
            targetAttrs.SetAttribute<Shield>(new Shield(0));
            _attrManager.Register(_targetId, targetAttrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);

            _comboLog = new ComboLogService();
            _comboLog.Register();

            var pipeline = new DamagePipeline(_attrManager);
            ServiceLocator.AddService<IDamagePipeline>(pipeline);
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void ApplyEffect_SameComboTwoTurnsInARow_SecondHitDealsZero()
        {
            var eff = CreateConstantEffect(30);

            // Turno 1: CombatHandoffService.DoConfirm registra el combo ANTES de
            // ejecutar el behavior — mismo orden acá.
            _comboLog.Record("combo.trio");
            var ctx1 = MakeCtx("combo.trio");
            eff.ApplyEffect(ctx1);

            int hpAfterFirst = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(970, hpAfterFirst, "Primer trio debe pegar el daño completo (30).");

            // Turno 2: mismo combo otra vez.
            _comboLog.Record("combo.trio");
            var ctx2 = MakeCtx("combo.trio");
            eff.ApplyEffect(ctx2);

            int hpAfterSecond = _attrManager.GetAttribute<Health>(_targetId).Value;
            Assert.AreEqual(hpAfterFirst, hpAfterSecond,
                "Repetir el mismo combo (trio) 2 veces seguidas debe anular el daño del segundo golpe.");
        }

        [Test]
        public void ApplyEffect_DifferentComboThenSame_BothHitsDealDamage()
        {
            var eff = CreateConstantEffect(30);

            _comboLog.Record("combo.doblepar");
            eff.ApplyEffect(MakeCtx("combo.doblepar"));
            int hpAfterFirst = _attrManager.GetAttribute<Health>(_targetId).Value;

            _comboLog.Record("combo.trio");
            eff.ApplyEffect(MakeCtx("combo.trio"));
            int hpAfterSecond = _attrManager.GetAttribute<Health>(_targetId).Value;

            Assert.Less(hpAfterSecond, hpAfterFirst,
                "Combo distinto al anterior debe pegar completo, no anularse.");
        }

        private EffDealDamage CreateConstantEffect(int amount)
        {
            var eff = new EffDealDamage();
            SetField(eff, "_damageSource", DamageSource.Constant);
            SetField(eff, "_baseAmount", amount);
            return eff;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private EffectContext MakeCtx(string comboId) => new EffectContext
        {
            SourceGuid = _sourceId,
            TargetGuid = _targetId,
            lastResult = true,
            ComboResult = ComboDetectionResult.Match(comboId, 30, 3, null),
        };
    }
}
