using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Concretes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Latch de "Instinto de Supervivencia": al estar en o bajo el umbral (% del máximo),
    /// +Attack hasta el fin del combate, una vez por combate, curarse no lo saca.
    /// </summary>
    public sealed class EffThresholdCrossCombatBuffTests
    {
        AttributesManager _attrMgr;
        Guid _player;
        Health _health;
        Attack _attack;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _player = Guid.NewGuid();
            _attrMgr = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrMgr);

            _health = new Health(100);
            _attack = new Attack(5);
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Health>(_health);
            attrs.SetAttribute<Attack>(_attack);
            attrs.SetAttribute<MaxHealth>(new MaxHealth(100));
            _attrMgr.Register(_player, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrMgr?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        EffectContext Ctx() => new EffectContext
        {
            SourceGuid = _player,
            TargetGuid = _player,
            SourceItemId = "item.instinto.test",
        };

        static EffThresholdCrossCombatBuff NewEffect() => new EffThresholdCrossCombatBuff();

        [Test]
        public void BelowThreshold_LatchesAttackBuff()
        {
            // Arrange — 30 de 100 = exactamente el umbral default (0.3)
            _health.Value = 30;

            // Act
            Assert.IsTrue(NewEffect().Apply(Ctx()));

            // Assert
            Assert.AreEqual(15, _attack.ModifiedValue, "Attack 5 + buff 10");
        }

        [Test]
        public void AboveThreshold_DoesNothing()
        {
            _health.Value = 31;

            Assert.IsTrue(NewEffect().Apply(Ctx()));

            Assert.AreEqual(5, _attack.ModifiedValue);
        }

        [Test]
        public void HealingBackAboveThreshold_DoesNotRemoveTheLatch()
        {
            var eff = NewEffect();
            _health.Value = 20;
            eff.Apply(Ctx());

            // Act — curarse por encima del umbral y re-evaluar (llega otro health-change)
            _health.Value = 90;
            eff.Apply(Ctx());

            // Assert — es un latch, no un maintainer (diferencia con EffLowHpAttackBuff)
            Assert.AreEqual(15, _attack.ModifiedValue);
        }

        [Test]
        public void SecondCrossInSameCombat_DoesNotStack()
        {
            var eff = NewEffect();
            _health.Value = 20;
            eff.Apply(Ctx());
            _health.Value = 10;

            eff.Apply(Ctx());

            Assert.AreEqual(15, _attack.ModifiedValue, "una vez por combate: el latch es el estado");
        }

        [Test]
        public void CombatEnd_RemovesBuff_AndCanLatchAgainNextCombat()
        {
            var eff = NewEffect();
            _health.Value = 20;
            eff.Apply(Ctx());
            Assert.AreEqual(15, _attack.ModifiedValue);

            // Act — fin de combate: el lifetime Encounter se auto-remueve
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert
            Assert.AreEqual(5, _attack.ModifiedValue, "Encounter lifetime limpia al cerrar el combate");

            // Próximo combate: vuelve a latchear
            eff.Apply(Ctx());
            Assert.AreEqual(15, _attack.ModifiedValue);
        }

        [Test]
        public void WithoutSourceItemId_FailsWithWarning()
        {
            _health.Value = 20;
            var ctx = Ctx();
            ctx.SourceItemId = null;

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("EffThresholdCrossCombatBuff.*SourceItemId"));
            Assert.IsFalse(NewEffect().Apply(ctx));
            Assert.AreEqual(5, _attack.ModifiedValue);
        }

        [Test]
        public void TwoItems_LatchIndependently()
        {
            _health.Value = 20;
            var ctxA = Ctx();
            var ctxB = Ctx();
            ctxB.SourceItemId = "item.otro.test";

            NewEffect().Apply(ctxA);
            NewEffect().Apply(ctxB);

            Assert.AreEqual(25, _attack.ModifiedValue,
                "cada item latchea con su propio SourceId — no se pisan entre sí");
        }
    }
}
