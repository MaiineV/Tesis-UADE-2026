using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// Tests de <see cref="BleedService"/>: stacks que SE SUMAN (a diferencia de Veneno),
    /// 10 daño por stack tickeando al inicio del turno del sangrante, expiración por stack
    /// tras 3 ticks, y teardown por scope.
    /// </summary>
    [TestFixture]
    public class BleedServiceTests
    {
        private BleedService _svc;
        private SpyDamagePipeline _damage;
        private Guid _entity;
        private Guid _source;

        private List<object[]> _appliedLog;
        private List<object[]> _tickedLog;
        private List<object[]> _expiredLog;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _entity = Guid.NewGuid();
            _source = Guid.NewGuid();

            _appliedLog = new List<object[]>();
            _tickedLog = new List<object[]>();
            _expiredLog = new List<object[]>();
            EventManager.Subscribe(EventName.OnBleedApplied, args => _appliedLog.Add(args));
            EventManager.Subscribe(EventName.OnBleedTicked, args => _tickedLog.Add(args));
            EventManager.Subscribe(EventName.OnBleedExpired, args => _expiredLog.Add(args));

            _svc = new BleedService();
            _svc.ConfigureForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void StartTurn(Guid entity) => EventManager.Trigger(EventName.OnTurnStarted, entity);

        [Test]
        public void AddStack_SetsOneStack_AndIsBleeding()
        {
            _svc.AddStack(_entity, _source);

            Assert.IsTrue(_svc.IsBleeding(_entity));
            Assert.AreEqual(1, _svc.GetStacks(_entity));
            Assert.AreEqual(1, _appliedLog.Count);
        }

        [Test]
        public void AddStack_Twice_StacksAdd_DoesNotRefresh()
        {
            _svc.AddStack(_entity, _source);
            _svc.AddStack(_entity, _source);

            Assert.AreEqual(2, _svc.GetStacks(_entity), "Sangrado ACUMULA, no refresca como Veneno.");
        }

        [Test]
        public void TurnStart_TicksDamageEqualToTenTimesStacks()
        {
            _svc.AddStack(_entity, _source);
            _svc.AddStack(_entity, _source);

            StartTurn(_entity);

            Assert.AreEqual(1, _damage.Resolved.Count, "un solo golpe de pipeline por turno, agregado.");
            Assert.AreEqual(20, _damage.Resolved[0].BaseDamage, "10 x 2 stacks vivos.");
            Assert.AreEqual(_entity, _damage.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.DamageOverTime, _damage.Resolved[0].Kind);
        }

        [Test]
        public void Stack_ExpiresAfterExactlyThreeTicks()
        {
            _svc.AddStack(_entity, _source);

            StartTurn(_entity);
            StartTurn(_entity);
            StartTurn(_entity);

            Assert.AreEqual(3, _damage.Resolved.Count);
            Assert.IsFalse(_svc.IsBleeding(_entity));
            Assert.AreEqual(1, _expiredLog.Count);

            StartTurn(_entity); // ya expirado: no debe tickear de más
            Assert.AreEqual(3, _damage.Resolved.Count);
        }

        [Test]
        public void StaggeredStacks_ExpireIndependently()
        {
            _svc.AddStack(_entity, _source); // stack A: nace en T0
            StartTurn(_entity); // A: 2 restantes
            _svc.AddStack(_entity, _source); // stack B: nace en T1, 3 restantes

            Assert.AreEqual(2, _svc.GetStacks(_entity));

            StartTurn(_entity); // A: 1 restante, B: 2 restantes — 2 stacks vivos = 20 daño
            Assert.AreEqual(20, _damage.Resolved[_damage.Resolved.Count - 1].BaseDamage);

            StartTurn(_entity); // A expira (0), B: 1 restante — daño de este tick = 20 (A todavía vivo al empezar)
            Assert.AreEqual(1, _svc.GetStacks(_entity), "solo B sigue vivo tras el 3er tick de A.");
        }

        [Test]
        public void OtherEntityTurnStart_DoesNotTick()
        {
            _svc.AddStack(_entity, _source);

            StartTurn(Guid.NewGuid());

            Assert.AreEqual(0, _damage.Resolved.Count);
            Assert.AreEqual(1, _svc.GetStacks(_entity));
        }

        [Test]
        public void Clear_RemovesAllStacks_AndFiresExpired()
        {
            _svc.AddStack(_entity, _source);
            _svc.AddStack(_entity, _source);

            _svc.Clear(_entity);

            Assert.IsFalse(_svc.IsBleeding(_entity));
            Assert.AreEqual(1, _expiredLog.Count);
        }

        [Test]
        public void CombatEnd_ClearsAll_WithoutExpiredEvents()
        {
            _svc.AddStack(_entity, _source);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_svc.IsBleeding(_entity));
            Assert.AreEqual(0, _expiredLog.Count, "Teardown silencioso, como Poison/Stun.");
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
