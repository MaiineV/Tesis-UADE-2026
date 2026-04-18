using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Stubs;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
using UnityEngine;

namespace Rollgeon.Entities.Bosses.Tests
{
    /// <summary>
    /// Tests de <see cref="BossAttackBehavior"/>: lee energia, aplica dano stub sobre Health,
    /// duplica si el rng cae bajo la chance cuando la energia esta llena.
    /// </summary>
    [TestFixture]
    public class BossAttackBehaviorTests
    {
        private AttributesManager _attrs;
        private BossFloorManagerSO _bossSO;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _bossSO = ScriptableObject.CreateInstance<BossFloorManagerSO>();
            _bossSO.BossEnergyMax = 4;
            _bossSO.DoubleDamageChanceDefault = 0f;
            _bossSO.DoubleDamageChanceWhenEnergyFull = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            Object.DestroyImmediate(_bossSO);
            ServiceLocator.Clear();
        }

        private sealed class TestCtx : BehaviorContext { }

        private (BehaviorContext ctx, Guid bossGuid, Guid targetGuid) SpawnActors(int targetHp)
        {
            var bossGuid = Guid.NewGuid();
            var targetGuid = Guid.NewGuid();

            var bossAttrs = new ModifiableAttributes();
            bossAttrs.EnsureInitialized();
            bossAttrs.SetAttribute<Health>(new Health(50));
            _attrs.Register(bossGuid, bossAttrs);

            var targetAttrs = new ModifiableAttributes();
            targetAttrs.EnsureInitialized();
            targetAttrs.SetAttribute<Health>(new Health(targetHp));
            _attrs.Register(targetGuid, targetAttrs);

            var ctx = new TestCtx { SourceEntity = new Entity { Guid = bossGuid } };
            return (ctx, bossGuid, targetGuid);
        }

        [Test]
        public void Execute_AppliesBaseDamage_WhenEnergyNotFull()
        {
            var (ctx, _, target) = SpawnActors(100);
            var b = new BossAttackBehavior
            {
                BossDataOverride = _bossSO,
                BaseAttackPower = 10,
                EnergyProbe = () => 0,
                EnergyMaxProbe = () => 4,
                RandomSource = () => 0.9f, // altisimo — nunca dispara doble dano.
                TargetGuid = target,
            };

            b.Execute(ctx);

            Assert.AreEqual(90, _attrs.GetAttributeValue<Health, int>(target));
        }

        [Test]
        public void Execute_AppliesDoubleDamage_WhenEnergyFull_AndRollUnderChance()
        {
            var (ctx, _, target) = SpawnActors(100);
            var b = new BossAttackBehavior
            {
                BossDataOverride = _bossSO,
                BaseAttackPower = 10,
                EnergyProbe = () => 4, // full.
                EnergyMaxProbe = () => 4,
                RandomSource = () => 0.1f, // < 0.5 → doble dano.
                TargetGuid = target,
            };

            b.Execute(ctx);

            Assert.AreEqual(80, _attrs.GetAttributeValue<Health, int>(target),
                "Energia llena + rng < 0.5 → dano x2.");
        }

        [Test]
        public void Execute_NoDoubleDamage_WhenEnergyFull_ButRollOverChance()
        {
            var (ctx, _, target) = SpawnActors(100);
            var b = new BossAttackBehavior
            {
                BossDataOverride = _bossSO,
                BaseAttackPower = 10,
                EnergyProbe = () => 4,
                EnergyMaxProbe = () => 4,
                RandomSource = () => 0.75f, // > 0.5 → no doble dano.
                TargetGuid = target,
            };

            b.Execute(ctx);

            Assert.AreEqual(90, _attrs.GetAttributeValue<Health, int>(target));
        }

        [Test]
        public void Execute_NoTarget_Noop()
        {
            var (ctx, _, _) = SpawnActors(100);
            var b = new BossAttackBehavior
            {
                BossDataOverride = _bossSO,
                BaseAttackPower = 10,
                TargetGuid = Guid.Empty,
            };
            Assert.DoesNotThrow(() => b.Execute(ctx));
        }

        [Test]
        public void Execute_ClampsHealthAtZero()
        {
            var (ctx, _, target) = SpawnActors(5);
            var b = new BossAttackBehavior
            {
                BossDataOverride = _bossSO,
                BaseAttackPower = 100,
                EnergyProbe = () => 0,
                EnergyMaxProbe = () => 4,
                RandomSource = () => 0.9f,
                TargetGuid = target,
            };

            b.Execute(ctx);

            Assert.AreEqual(0, _attrs.GetAttributeValue<Health, int>(target));
        }
    }
}
