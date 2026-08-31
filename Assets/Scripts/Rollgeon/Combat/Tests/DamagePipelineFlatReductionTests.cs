using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Tests
{
    /// <summary>Stage 3b del pipeline: reducción plana entrante (aura de Guardian) con piso 1,
    /// idéntica en Resolve y Preview.</summary>
    [TestFixture]
    public class DamagePipelineFlatReductionTests
    {
        private sealed class StubReducer : IIncomingFlatDamageReducerProvider
        {
            public int Reduction;
            public int GetFlatReduction(DamageContext ctx) => Reduction;
        }

        private AttributesManager _attributes;
        private DamagePipeline _pipeline;
        private StubReducer _reducer;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attributes = new AttributesManager();
            _pipeline = new DamagePipeline(_attributes);

            _reducer = new StubReducer();
            ServiceLocator.AddService<IIncomingFlatDamageReducerProvider>(_reducer, ServiceScope.Global);

            _target = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(100));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(_target, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private DamageContext Hit(int baseDamage) => new DamageContext
        {
            SourceId = Guid.NewGuid(),
            TargetId = _target,
            BaseDamage = baseDamage,
            Kind = AttackKind.BasicAttack,
        };

        [Test]
        public void Resolve_FlatReduction_SubtractsAndRecords()
        {
            _reducer.Reduction = 4;

            var ctx = _pipeline.Resolve(Hit(10));

            Assert.AreEqual(6, ctx.FinalDamage);
            Assert.AreEqual(4, ctx.IncomingFlatReduction);
            Assert.AreEqual(94, _attributes.GetAttribute<Health>(_target).Value);
        }

        [Test]
        public void Resolve_ReductionAboveDamage_FloorsAtOne()
        {
            _reducer.Reduction = 50;

            var ctx = _pipeline.Resolve(Hit(10));

            Assert.AreEqual(1, ctx.FinalDamage, "un golpe que entró positivo nunca muestra 0");
            Assert.AreEqual(99, _attributes.GetAttribute<Health>(_target).Value);
        }

        [Test]
        public void Resolve_ZeroReduction_IsNoOp()
        {
            _reducer.Reduction = 0;

            var ctx = _pipeline.Resolve(Hit(10));

            Assert.AreEqual(10, ctx.FinalDamage);
            Assert.AreEqual(0, ctx.IncomingFlatReduction);
        }

        [Test]
        public void Preview_MatchesResolve_WithoutWritingHealth()
        {
            _reducer.Reduction = 4;

            var preview = _pipeline.Preview(Hit(10));

            Assert.AreEqual(6, preview.FinalDamage, "el desglose no puede driftear del golpe real");
            Assert.AreEqual(100, _attributes.GetAttribute<Health>(_target).Value, "Preview no escribe");
        }
    }
}
