using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Editor.Tools.Enemy.Templates;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Feedback;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EffectAuthoringTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        static bool HasField(System.Type t, string name)
        {
            for (; t != null; t = t.BaseType) if (t.GetField(name, Private) != null) return true;
            return false;
        }

        [Test]
        public void PrivateFieldsStillExist_OnRuntimeEffects()
        {
            // Guardián: si el runtime renombra un campo, las plantillas dejarían de autorarlo en silencio.
            foreach (var f in EffectAuthoring.DealDamageFields) Assert.IsTrue(HasField(typeof(EffDealDamage), f), f);
            foreach (var f in EffectAuthoring.ModifyIntFields) Assert.IsTrue(HasField(typeof(EffModifyIntAttribute), f), f);
            foreach (var f in EffectAuthoring.PlaySequenceFields) Assert.IsTrue(HasField(typeof(EffPlaySequence), f), f);
        }

        [Test]
        public void DealDamageFromStat_ReadsSourceAttack()
        {
            var e = EffectAuthoring.DealDamageFromStat(StatType.Attack, 0.5f);
            Assert.AreEqual(DamageSource.FromReader, EffectAuthoring.Get(e, "_damageSource"));
            var reader = EffectAuthoring.Get(e, "_reader") as ReadEntityStat;
            Assert.IsNotNull(reader);
            Assert.AreEqual(ReaderEntitySource.Source, reader.Entity);
            Assert.AreEqual(StatType.Attack, reader.Stat);
            Assert.AreEqual(0.5f, (float)EffectAuthoring.Get(e, "_readerMultiplier"));
        }

        [Test]
        public void ModifyStat_IsConstantAmount()
        {
            var e = EffectAuthoring.ModifyStat(StatType.Energy, IntOperation.Set, 3);
            Assert.AreEqual(StatType.Energy, e.TargetStat);
            Assert.AreEqual(IntOperation.Set, e.Operation);
            Assert.AreEqual(DamageSource.Constant, EffectAuthoring.Get(e, "_amountSource"));
            Assert.AreEqual(3, (int)EffectAuthoring.Get(e, "_baseAmount"));
        }

        [Test]
        public void HealFromStat_AddsHealthClampedFromHealStrength()
        {
            var e = EffectAuthoring.HealFromStat();
            Assert.AreEqual(StatType.Health, e.TargetStat);
            Assert.AreEqual(IntOperation.Add, e.Operation);
            Assert.IsTrue((bool)EffectAuthoring.Get(e, "_clampHealthToMax"));
            Assert.AreEqual(StatType.HealStrength, ((ReadEntityStat)EffectAuthoring.Get(e, "_reader")).Stat);
        }

        [Test]
        public void Sequence_KeepsStepsInOrder()
        {
            var e = EffectAuthoring.Sequence(
                EffectAuthoring.Step("a", StepEndMode.OnEvent, "hit"),
                EffectAuthoring.Step("b"));
            Assert.AreEqual(2, e.Steps.Count);
            Assert.AreEqual("a", e.Steps[0].FeedbackRefId);
            Assert.AreEqual(StepEndMode.OnEvent, e.Steps[0].EndMode);
            Assert.AreEqual("hit", e.Steps[0].EndOnEventKey);
            Assert.AreEqual("b", e.Steps[1].FeedbackRefId);
        }
    }
}
