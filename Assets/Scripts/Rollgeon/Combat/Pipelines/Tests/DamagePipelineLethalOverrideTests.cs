using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Combat.Pipelines.Tests
{
    /// <summary>
    /// EditMode tests del hook <see cref="ILethalDamageOverride"/> en
    /// <see cref="DamagePipeline"/> (inmortalidad del tutorial).
    /// </summary>
    [TestFixture]
    public class DamagePipelineLethalOverrideTests
    {
        private AttributesManager _attrManager;
        private Guid _sourceId;
        private Guid _targetId;
        private DamageResolvedPayload? _lastPayload;

        private sealed class StubLethalOverride : ILethalDamageOverride
        {
            public Guid ProtectedId;
            public bool ShouldPreventLethal(Guid targetId) => targetId == ProtectedId;
        }

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();
            _targetId = Guid.NewGuid();

            var sourceAttrs = new ModifiableAttributes();
            sourceAttrs.EnsureInitialized();
            _attrManager.Register(_sourceId, sourceAttrs);

            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(100));
            _attrManager.Register(_targetId, targetAttrs);

            _lastPayload = null;
            TypedEvent<DamageResolvedPayload>.Subscribe(OnResolved);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<DamageResolvedPayload>.Unsubscribe(OnResolved);
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
        }

        private void OnResolved(DamageResolvedPayload payload) => _lastPayload = payload;

        private DamageContext Hit(int damage)
        {
            var pipeline = new DamagePipeline(_attrManager);
            return pipeline.Resolve(new DamageContext
            {
                SourceId = _sourceId,
                TargetId = _targetId,
                BaseDamage = damage,
                Kind = AttackKind.BasicAttack,
            });
        }

        private void RegisterOverrideFor(Guid protectedId)
        {
            ServiceLocator.AddService<ILethalDamageOverride>(
                new StubLethalOverride { ProtectedId = protectedId }, ServiceScope.Run);
        }

        [Test]
        public void Resolve_LethalHitWithOverride_ClampsToRemainingHpAndNotLethal()
        {
            RegisterOverrideFor(_targetId);

            var ctx = Hit(250);

            Assert.AreEqual(DamagePipeline.LethalOverrideRemainingHp,
                _attrManager.GetAttribute<Health>(_targetId).Value,
                "El golpe letal debe dejar al target con el resto de vida del override.");
            Assert.IsFalse(ctx.WasLethal);
            Assert.IsFalse(_lastPayload.Value.WasLethal,
                "El payload no debe marcar letal — CombatDeathWatcher no debe ver la muerte.");
            Assert.AreEqual(90, ctx.FinalDamage,
                "FinalDamage debe reflejar el daño efectivamente aplicado (100 → 10).");
        }

        [Test]
        public void Resolve_ExactlyLethalHitWithOverride_ClampsToRemainingHp()
        {
            RegisterOverrideFor(_targetId);

            var ctx = Hit(100);

            Assert.AreEqual(DamagePipeline.LethalOverrideRemainingHp,
                _attrManager.GetAttribute<Health>(_targetId).Value);
            Assert.IsFalse(ctx.WasLethal);
        }

        [Test]
        public void Resolve_NonLethalHitWithOverride_AppliesFullDamage()
        {
            RegisterOverrideFor(_targetId);

            var ctx = Hit(40);

            Assert.AreEqual(60, _attrManager.GetAttribute<Health>(_targetId).Value,
                "Daño no-letal se aplica completo — el override solo actúa al llegar a 0.");
            Assert.AreEqual(40, ctx.FinalDamage);
        }

        [Test]
        public void Resolve_LethalHitWithOverride_TargetAlreadyBelowRemaining_DoesNotHeal()
        {
            // El override no debe CURAR: si el target ya estaba por debajo del resto
            // (10), el golpe letal lo deja donde estaba.
            RegisterOverrideFor(_targetId);
            _attrManager.SetAttributeValue<Health, int>(_targetId, 5);

            var ctx = Hit(50);

            Assert.AreEqual(5, _attrManager.GetAttribute<Health>(_targetId).Value);
            Assert.IsFalse(ctx.WasLethal);
            Assert.AreEqual(0, ctx.FinalDamage);
        }

        [Test]
        public void Resolve_LethalHitOverrideForOtherTarget_StaysLethal()
        {
            RegisterOverrideFor(Guid.NewGuid());

            var ctx = Hit(250);

            Assert.AreEqual(0, _attrManager.GetAttribute<Health>(_targetId).Value);
            Assert.IsTrue(ctx.WasLethal);
            Assert.IsTrue(_lastPayload.Value.WasLethal);
        }

        [Test]
        public void Resolve_LethalHitWithoutOverride_StaysLethal()
        {
            var ctx = Hit(250);

            Assert.AreEqual(0, _attrManager.GetAttribute<Health>(_targetId).Value);
            Assert.IsTrue(ctx.WasLethal);
        }
    }
}
