using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Effects.Selection.Queries;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Sirenix.Serialization;

namespace Rollgeon.Effects.Tests
{
    // --------------------------------------------------------------------------------
    // Test helpers — minimal concretes to exercise the pipeline in isolation.
    // --------------------------------------------------------------------------------

    [Serializable]
    public class PC_AlwaysTrue : BasePreCondition
    {
        public override string ConditionName => "AlwaysTrue";
        public override bool Evaluate(PreConditionContext context) => true;
    }

    [Serializable]
    public class PC_AlwaysFalse : BasePreCondition
    {
        public override string ConditionName => "AlwaysFalse";
        public override bool Evaluate(PreConditionContext context) => false;
    }

    /// <summary>Effect de prueba que cuenta ejecuciones y devuelve lo que se le configure.</summary>
    [Serializable]
    public class Eff_ReturnsConfigured : BaseEffect
    {
        public bool ReturnValue = true;
        public int ExecutionCount;
        public string Tag;

        public override bool ApplyEffect(EffectContext context)
        {
            ExecutionCount++;
            return ReturnValue;
        }
    }

    /// <summary>Behavior stub concreto, instanciable (BaseBehavior es abstract).</summary>
    public class TestBehavior : BaseBehavior
    {
        public override void Execute(BehaviorContext ctx) { }
    }

    // Contextos polimórficos para test del TryGetTriggerContext<T>.
    public class FooBehaviorContext : BehaviorContext
    {
        public int FooValue;
    }

    public class BarBehaviorContext : BehaviorContext
    {
        public string BarLabel;
    }

    // --------------------------------------------------------------------------------
    // Tests.
    // --------------------------------------------------------------------------------

    [TestFixture]
    public class EffectsPipelineTests
    {
        private EffectContext MakeCtx(BaseBehavior bh = null)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.Empty,
                lastResult = true,
                SourceBehavior = bh,
            };
        }

        private PreConditionContext MakePreCtx()
        {
            return new PreConditionContext
            {
                OwnerGuid = Guid.NewGuid(),
                OpponentGuid = Guid.Empty,
            };
        }

        // ───── Plan §3.6 scenario 1 ───────────────────────────────────────────────
        [Test]
        public void EffectData_TryExecute_PreConditionsFail_DoesNotExecuteEffects()
        {
            var eff = new Eff_ReturnsConfigured { ReturnValue = true };
            var data = new EffectData
            {
                PreConditions = new List<BasePreCondition> { new PC_AlwaysFalse() },
                Effects = new List<IEffect> { eff },
            };

            var ok = data.TryExecute(MakeCtx(), MakePreCtx());

            Assert.IsFalse(ok, "TryExecute should return false when preconditions fail");
            Assert.AreEqual(0, eff.ExecutionCount, "Effect must not run when preconditions fail");
        }

        // ───── Plan §3.6 scenario 2 ───────────────────────────────────────────────
        [Test]
        public void EffectData_TryExecute_AllPreConditionsPass_ExecutesAllInOrder()
        {
            var a = new Eff_ReturnsConfigured { ReturnValue = true, Tag = "a" };
            var b = new Eff_ReturnsConfigured { ReturnValue = true, Tag = "b" };
            var c = new Eff_ReturnsConfigured { ReturnValue = true, Tag = "c" };
            var data = new EffectData
            {
                PreConditions = new List<BasePreCondition> { new PC_AlwaysTrue(), new PC_AlwaysTrue() },
                Effects = new List<IEffect> { a, b, c },
            };
            var ctx = MakeCtx();

            var ok = data.TryExecute(ctx, MakePreCtx());

            Assert.IsTrue(ok);
            Assert.AreEqual(1, a.ExecutionCount);
            Assert.AreEqual(1, b.ExecutionCount);
            Assert.AreEqual(1, c.ExecutionCount);
            Assert.AreEqual(2, ctx.EffectIndex, "EffectIndex should reach last");
        }

        // ───── Plan §3.6 scenario 3 — cortocircuito §8.8 ──────────────────────────
        [Test]
        public void EffectData_TryExecute_EffectReturnsFalse_ShortCircuitsRemainingEffects()
        {
            var a = new Eff_ReturnsConfigured { ReturnValue = true };
            var stopper = new Eff_ReturnsConfigured { ReturnValue = false };
            var c = new Eff_ReturnsConfigured { ReturnValue = true };
            var data = new EffectData
            {
                PreConditions = new List<BasePreCondition>(),
                Effects = new List<IEffect> { a, stopper, c },
            };

            var ok = data.TryExecute(MakeCtx(), MakePreCtx());

            Assert.IsFalse(ok, "TryExecute returns ctx.lastResult, which must be false after short-circuit");
            Assert.AreEqual(1, a.ExecutionCount);
            Assert.AreEqual(1, stopper.ExecutionCount);
            Assert.AreEqual(0, c.ExecutionCount, "Effect after stopper must be short-circuited");
        }

        // ───── Plan §3.6 scenario 4 ───────────────────────────────────────────────
        [Test]
        public void BaseTargetQuery_TQSelf_ReturnsOwnerAsTarget()
        {
            var ownerPos = new GridCoord(3, 5);
            var q = new TQ_Self();
            var ctx = new TargetQueryContext { OwnerGuid = Guid.NewGuid(), OwnerPosition = ownerPos };

            var result = q.Evaluate(ctx);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(ownerPos, result[0].Coord);
        }

        // ───── Plan §3.6 scenario 5 ───────────────────────────────────────────────
        [Test]
        public void BaseEffect_ApplySelectionValidation_FailsWhenCancelledAndNotSkippable()
        {
            var eff = new Eff_ReturnsConfigured();
            eff.Selection = new SelectionSettings
            {
                RequiresSelection = true,
                IsSkippable = false,
                SelectionCount = 1,
                TargetQuery = new TQ_Self(),
            };

            var cancelled = new TargetSelectionResult { WasCancelled = true };
            var passed = eff.ValidateSelection(cancelled, Guid.NewGuid(), out var err);

            Assert.IsFalse(passed);
            Assert.IsNotNull(err);
            StringAssert.Contains("cancelled", err.ToLowerInvariant());

            // Skippable → cancelled es aceptable.
            eff.Selection.IsSkippable = true;
            var skipPassed = eff.ValidateSelection(cancelled, Guid.NewGuid(), out _);
            Assert.IsTrue(skipPassed);
        }

        // ───── Plan §3.6 scenario 6 — truth table AND/OR/NOT ──────────────────────
        [Test]
        public void PCComposite_AndOrNot_EvaluationMatrix()
        {
            var T = new PC_AlwaysTrue();
            var F = new PC_AlwaysFalse();
            var ctx = MakePreCtx();

            // AND
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.And, Children = new List<BasePreCondition> { T, T } }.Evaluate(ctx));
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.And, Children = new List<BasePreCondition> { T, F } }.Evaluate(ctx));
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.And, Children = new List<BasePreCondition> { F, F } }.Evaluate(ctx));
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.And, Children = new List<BasePreCondition>() }.Evaluate(ctx), "empty AND is vacuously true");

            // OR
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.Or, Children = new List<BasePreCondition> { T, T } }.Evaluate(ctx));
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.Or, Children = new List<BasePreCondition> { T, F } }.Evaluate(ctx));
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.Or, Children = new List<BasePreCondition> { F, T } }.Evaluate(ctx));
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.Or, Children = new List<BasePreCondition> { F, F } }.Evaluate(ctx));
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.Or, Children = new List<BasePreCondition>() }.Evaluate(ctx), "empty OR has no one to approve");

            // NOT == NAND
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.Not, Children = new List<BasePreCondition> { T } }.Evaluate(ctx), "NOT(T) == F");
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.Not, Children = new List<BasePreCondition> { F } }.Evaluate(ctx), "NOT(F) == T");
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.Not, Children = new List<BasePreCondition> { T, T } }.Evaluate(ctx), "NAND(T,T) == F");
            Assert.IsTrue(new PCComposite { Mode = CompositeMode.Not, Children = new List<BasePreCondition> { T, F } }.Evaluate(ctx), "NAND(T,F) == T");
            Assert.IsFalse(new PCComposite { Mode = CompositeMode.Not, Children = new List<BasePreCondition>() }.Evaluate(ctx), "empty NOT → !true(vacuous AND) = false");
        }

        // ───── TryGetTriggerContext — tipado ──────────────────────────────────────
        [Test]
        public void EffectContext_TryGetTriggerContext_ReturnsTrueOnlyForMatchingSubtype()
        {
            var ctx = MakeCtx();
            ctx.TriggerContext = new FooBehaviorContext { FooValue = 42 };

            Assert.IsTrue(ctx.TryGetTriggerContext<FooBehaviorContext>(out var foo));
            Assert.AreEqual(42, foo.FooValue);

            Assert.IsFalse(ctx.TryGetTriggerContext<BarBehaviorContext>(out var bar));
            Assert.IsNull(bar);

            ctx.TriggerContext = null;
            Assert.IsFalse(ctx.TryGetTriggerContext<FooBehaviorContext>(out _));
        }

        // ───── Plan §3.6 scenario 7 CRITICAL — polymorphic round-trip with Odin ───
        [Test]
        public void EffectData_PolymorphicRoundTrip_WithOdin()
        {
            // Armar un EffectData con subtipos concretos mixtos — PCComposite anidado,
            // EffDealDamage + EffHeal, SelectionSettings con TQ_Self.
            var original = new EffectData
            {
                Label = "Roundtrip",
                PreConditions = new List<BasePreCondition>
                {
                    new PCComposite
                    {
                        Mode = CompositeMode.Or,
                        Children = new List<BasePreCondition>
                        {
                            new PC_AlwaysTrue(),
                            new PC_AlwaysFalse(),
                        }
                    }
                },
                Effects = new List<IEffect>
                {
                    new EffDealDamage(),
                    new EffHeal(),
                },
            };

            // EffDealDamage lleva Selection con su propio TargetQuery para forzar round-trip
            // polimórfico del campo inline de SelectionSettings (§13.6.1 + §11.2).
            var damage = (EffDealDamage)original.Effects[0];
            damage.Selection = new SelectionSettings
            {
                RequiresSelection = true,
                SelectionCount = 3,
                TargetQuery = new TQ_Self(),
            };

            var bytes = SerializationUtility.SerializeValue(original, DataFormat.JSON);
            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 0);

            var restored = SerializationUtility.DeserializeValue<EffectData>(bytes, DataFormat.JSON);

            // Validaciones — los subtipos concretos sobreviven.
            Assert.IsNotNull(restored);
            Assert.AreEqual("Roundtrip", restored.Label);

            Assert.AreEqual(1, restored.PreConditions.Count);
            Assert.IsInstanceOf<PCComposite>(restored.PreConditions[0]);
            var composite = (PCComposite)restored.PreConditions[0];
            Assert.AreEqual(CompositeMode.Or, composite.Mode);
            Assert.AreEqual(2, composite.Children.Count);
            Assert.IsInstanceOf<PC_AlwaysTrue>(composite.Children[0]);
            Assert.IsInstanceOf<PC_AlwaysFalse>(composite.Children[1]);

            Assert.AreEqual(2, restored.Effects.Count);
            Assert.IsInstanceOf<EffDealDamage>(restored.Effects[0]);
            Assert.IsInstanceOf<EffHeal>(restored.Effects[1]);

            // El SelectionSettings dentro de EffDealDamage también sobrevivió.
            var restoredDmg = (EffDealDamage)restored.Effects[0];
            Assert.IsNotNull(restoredDmg.Selection);
            Assert.IsTrue(restoredDmg.Selection.RequiresSelection);
            Assert.AreEqual(3, restoredDmg.Selection.SelectionCount);
            Assert.IsInstanceOf<TQ_Self>(restoredDmg.Selection.TargetQuery);
        }

        // ───── BaseBehavior stub — SetBehaviorValue / TryGetBehaviorValues ────────
        [Test]
        public void BaseBehavior_StoredValues_SetGetClearRoundTrip()
        {
            var bh = new TestBehavior();
            bh.SetBehaviorValue(BehaviorValueKey.FloatingDamage, new FloatingNumberBehaviorValue { Value = 10f });
            bh.SetBehaviorValue(BehaviorValueKey.FloatingDamage, new FloatingNumberBehaviorValue { Value = 15f });
            bh.SetBehaviorValue(BehaviorValueKey.FloatingHeal, new FloatingNumberBehaviorValue { Value = 5f });

            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingDamage, out var dmgs));
            Assert.AreEqual(2, dmgs.Count);

            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingHeal, out var heals));
            Assert.AreEqual(1, heals.Count);

            bh.ClearBehaviorValues();
            Assert.IsFalse(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingDamage, out _));
        }

        // ───── EffDealDamage integration — example effect writes storedValue ──────────
        [Test]
        public void EffDealDamage_Example_WritesFloatingDamageToBehavior()
        {
            var bh = new TestBehavior();
            var ctx = MakeCtx(bh);
            ctx.TargetGuid = Guid.NewGuid();

            var eff = new EffDealDamage();
            var data = new EffectData { Effects = new List<IEffect> { eff } };

            var ok = data.TryExecute(ctx, MakePreCtx());

            Assert.IsTrue(ok);
            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingDamage, out var list));
            Assert.AreEqual(1, list.Count);
            Assert.Greater(list[0].Value, 0);
        }
    }
}
