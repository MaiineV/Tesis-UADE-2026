using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// Tests de <see cref="PoisonService"/>: 5 daño × 3 turnos tickeando al inicio del turno
    /// del envenenado, refresh-sin-stack, expiración exacta, y teardown por scope.
    /// </summary>
    [TestFixture]
    public class PoisonServiceTests
    {
        private PoisonService _svc;
        private SpyDamagePipeline _damage;
        private Guid _entity;
        private Guid _source;

        private List<object[]> _tickedLog;
        private List<object[]> _expiredLog;
        private EventManager.EventReceiver _onTicked;
        private EventManager.EventReceiver _onExpired;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _damage = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _entity = Guid.NewGuid();
            _source = Guid.NewGuid();

            _tickedLog = new List<object[]>();
            _expiredLog = new List<object[]>();
            _onTicked = args => _tickedLog.Add(args);
            _onExpired = args => _expiredLog.Add(args);
            EventManager.Subscribe(EventName.OnPoisonTicked, _onTicked);
            EventManager.Subscribe(EventName.OnPoisonExpired, _onExpired);

            _svc = new PoisonService();
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
        public void ApplyPoison_SetsThreeTurns_AndIsPoisoned()
        {
            _svc.ApplyPoison(_entity, damagePerTurn: 5, turns: 3, _source);

            Assert.IsTrue(_svc.IsPoisoned(_entity));
            Assert.AreEqual(3, _svc.GetPoisonTurns(_entity));
        }

        [Test]
        public void TurnStart_TicksFiveDamageOverTime_WithTileSourceId()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);

            StartTurn(_entity);

            Assert.AreEqual(1, _damage.Resolved.Count);
            Assert.AreEqual(5, _damage.Resolved[0].BaseDamage);
            Assert.AreEqual(_source, _damage.Resolved[0].SourceId);
            Assert.AreEqual(_entity, _damage.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.DamageOverTime, _damage.Resolved[0].Kind);
            Assert.AreEqual(2, _svc.GetPoisonTurns(_entity));
        }

        [Test]
        public void Poison_ExpiresAfterExactlyThreeTicks()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);

            StartTurn(_entity);
            StartTurn(_entity);
            StartTurn(_entity);
            StartTurn(_entity); // ya expirado: no debe tickear de más

            Assert.AreEqual(3, _damage.Resolved.Count, "Exactamente 3 ticks de 5 — ni uno más.");
            Assert.IsFalse(_svc.IsPoisoned(_entity));
            Assert.AreEqual(1, _expiredLog.Count);
            Assert.AreEqual(_entity, _expiredLog[0][0]);
        }

        [Test]
        public void ReApply_RefreshesDuration_DoesNotStack()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);
            StartTurn(_entity);
            StartTurn(_entity);
            Assert.AreEqual(1, _svc.GetPoisonTurns(_entity), "Setup: queda 1 turno.");

            _svc.ApplyPoison(_entity, 5, 3, _source);

            Assert.AreEqual(3, _svc.GetPoisonTurns(_entity),
                "Re-pisar Veneno refresca la duración a 3, nunca la acumula (no 1+3).");
            StartTurn(_entity);
            Assert.AreEqual(5, _damage.Resolved[_damage.Resolved.Count - 1].BaseDamage,
                "El daño por tick tampoco se acumula.");
        }

        [Test]
        public void OtherEntityTurnStart_DoesNotTick()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);

            StartTurn(Guid.NewGuid());

            Assert.AreEqual(0, _damage.Resolved.Count);
            Assert.AreEqual(3, _svc.GetPoisonTurns(_entity));
        }

        [Test]
        public void Clear_RemovesPoison_AndFiresExpired()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);

            _svc.Clear(_entity);

            Assert.IsFalse(_svc.IsPoisoned(_entity));
            Assert.AreEqual(1, _expiredLog.Count);
        }

        [Test]
        public void CombatEnd_ClearsAll_WithoutExpiredEvents()
        {
            _svc.ApplyPoison(_entity, 5, 3, _source);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_svc.IsPoisoned(_entity));
            Assert.AreEqual(0, _expiredLog.Count, "Teardown silencioso, como StunService.");
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
