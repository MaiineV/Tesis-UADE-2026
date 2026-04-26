using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    [TestFixture]
    public class ClassPassiveSOTests
    {
        private ClassPassiveSO _passive;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            _passive = ScriptableObject.CreateInstance<ClassPassiveSO>();
            _passive.PassiveId = "passive.test";
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            UnityEngine.Object.DestroyImmediate(_passive);
        }

        [Test]
        public void BindPassive_WithOneHook_SubscribesToEvent()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spy));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);

            Assert.AreEqual(1, spy.CallCount);
            entity.Dispose();
        }

        [Test]
        public void BindPassive_WithMultipleHooks_SubscribesAll()
        {
            var spyA = new SpyEffect();
            var spyB = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spyA));
            _passive.Hooks.Add(MakeHook(EventName.OnRollResolved, spyB));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);
            EventManager.Trigger(EventName.OnRollResolved, entity.InstanceId);

            Assert.AreEqual(1, spyA.CallCount);
            Assert.AreEqual(1, spyB.CallCount);
            entity.Dispose();
        }

        [Test]
        public void BindPassive_FiltersByInstanceId()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spy));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);

            var otherGuid = Guid.NewGuid();
            EventManager.Trigger(EventName.OnTurnStarted, otherGuid);

            Assert.AreEqual(0, spy.CallCount);
            entity.Dispose();
        }

        [Test]
        public void BindPassive_MatchingInstanceId_ExecutesEffect()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnDamageOutgoing, spy));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);

            EventManager.Trigger(EventName.OnDamageOutgoing, entity.InstanceId, entity.InstanceId, 10);

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(entity.InstanceId, spy.LastSourceGuid);
            entity.Dispose();
        }

        [Test]
        public void BindPassive_NullPassive_NoOp()
        {
            var entity = new Entity { InstanceId = Guid.NewGuid() };

            Assert.DoesNotThrow(() => entity.BindPassive(null));
            Assert.IsNull(entity.Passive);
            entity.Dispose();
        }

        [Test]
        public void BindPassive_CalledTwice_UnbindsPrevious()
        {
            var spyOld = new SpyEffect();
            var spyNew = new SpyEffect();

            var passiveOld = ScriptableObject.CreateInstance<ClassPassiveSO>();
            passiveOld.Hooks.Add(MakeHook(EventName.OnTurnStarted, spyOld));

            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spyNew));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(passiveOld);
            entity.BindPassive(_passive);

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);

            Assert.AreEqual(0, spyOld.CallCount);
            Assert.AreEqual(1, spyNew.CallCount);
            Assert.AreSame(_passive, entity.Passive);

            entity.Dispose();
            UnityEngine.Object.DestroyImmediate(passiveOld);
        }

        [Test]
        public void UnbindPassive_ClearsHandlers()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spy));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);
            entity.UnbindPassive();

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);

            Assert.AreEqual(0, spy.CallCount);
        }

        [Test]
        public void UnbindPassive_SetsPassiveNull()
        {
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, new SpyEffect()));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);
            entity.UnbindPassive();

            Assert.IsNull(entity.Passive);
        }

        [Test]
        public void Dispose_CallsUnbindPassive()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spy));

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);
            entity.Dispose();

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);

            Assert.AreEqual(0, spy.CallCount);
            Assert.IsNull(entity.Passive);
        }

        [Test]
        public void TwoEntities_SamePassive_NoCrossTalk()
        {
            var spy = new SpyEffect();
            _passive.Hooks.Add(MakeHook(EventName.OnTurnStarted, spy));

            var entityA = new Entity { InstanceId = Guid.NewGuid() };
            var entityB = new Entity { InstanceId = Guid.NewGuid() };
            entityA.BindPassive(_passive);
            entityB.BindPassive(_passive);

            EventManager.Trigger(EventName.OnTurnStarted, entityA.InstanceId);

            Assert.AreEqual(1, spy.CallCount);
            Assert.AreEqual(entityA.InstanceId, spy.LastSourceGuid);

            entityA.Dispose();
            entityB.Dispose();
        }

        [Test]
        public void Hook_WithFailingPreCondition_DoesNotExecuteEffects()
        {
            var spy = new SpyEffect();
            var hook = MakeHook(EventName.OnTurnStarted, spy);
            hook.Effect.PreConditions.Add(new AlwaysFailPC());
            _passive.Hooks.Add(hook);

            var entity = new Entity { InstanceId = Guid.NewGuid() };
            entity.BindPassive(_passive);

            EventManager.Trigger(EventName.OnTurnStarted, entity.InstanceId);

            Assert.AreEqual(0, spy.CallCount);
            entity.Dispose();
        }

        // --- Helpers ---

        private static PassiveHook MakeHook(EventName evt, IEffect effect)
        {
            var hook = new PassiveHook
            {
                TriggerEvent = evt,
                Effect = new EffectData()
            };
            hook.Effect.Effects.Add(effect);
            return hook;
        }

        [Serializable]
        private class SpyEffect : BaseEffect
        {
            public int CallCount;
            public Guid LastSourceGuid;

            public override bool ApplyEffect(EffectContext context)
            {
                CallCount++;
                LastSourceGuid = context.SourceGuid;
                return true;
            }
        }

        [Serializable]
        private class AlwaysFailPC : BasePreCondition
        {
            public override string ConditionName => "AlwaysFail";
            public override bool Evaluate(PreConditionContext context) => false;
        }
    }
}
