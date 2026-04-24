using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Energy;
using Rollgeon.Combos;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    [TestFixture]
    public class HeroBehaviorSetTests
    {
        [Test]
        public void IsValid_AllAssigned_ReturnsTrue()
        {
            var set = new HeroBehaviorSet();
            Assert.IsTrue(set.IsValid);
        }

        [Test]
        public void IsValid_MissingMovement_ReturnsFalse()
        {
            var set = new HeroBehaviorSet { Movement = null };
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void IsValid_MissingBaseAttack_ReturnsFalse()
        {
            var set = new HeroBehaviorSet { BaseAttack = null };
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void IsValid_MissingSpecialAttack_ReturnsFalse()
        {
            var set = new HeroBehaviorSet { SpecialAttack = null };
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void IsValid_MissingHealing_ReturnsFalse()
        {
            var set = new HeroBehaviorSet { Healing = null };
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void All_YieldsFour()
        {
            var set = new HeroBehaviorSet();
            var all = set.All.ToList();
            Assert.AreEqual(4, all.Count);
            Assert.AreSame(set.Movement, all[0]);
            Assert.AreSame(set.BaseAttack, all[1]);
            Assert.AreSame(set.SpecialAttack, all[2]);
            Assert.AreSame(set.Healing, all[3]);
        }

        [Test]
        public void GetByIndex_ReturnsCorrectSlot()
        {
            var set = new HeroBehaviorSet();
            Assert.AreSame(set.Movement, set.GetByIndex(0));
            Assert.AreSame(set.BaseAttack, set.GetByIndex(1));
            Assert.AreSame(set.SpecialAttack, set.GetByIndex(2));
            Assert.AreSame(set.Healing, set.GetByIndex(3));
            Assert.IsNull(set.GetByIndex(4));
            Assert.IsNull(set.GetByIndex(-1));
        }

        [Test]
        public void ClassHeroSO_Actions_DefaultNotNull()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                Assert.IsNotNull(so.Actions);
                Assert.IsTrue(so.Actions.IsValid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void ClassHeroSO_ContextualBehaviors_DefaultEmpty()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                Assert.IsNotNull(so.ContextualBehaviors);
                Assert.AreEqual(0, so.ContextualBehaviors.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }
    }

    [TestFixture]
    public class HeroActionBehaviorTests
    {
        [Test]
        public void Execute_RunsEffectPipeline()
        {
            var tracker = new EffectTracker();
            var behavior = new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData>
                {
                    CreateEffectDataWith(tracker),
                }
            };

            behavior.Execute(new TestBehaviorContext());

            Assert.AreEqual(1, tracker.ApplyCount);
        }

        [Test]
        public void Execute_MultipleGroups_RunsAll()
        {
            var tracker1 = new EffectTracker();
            var tracker2 = new EffectTracker();
            var behavior = new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData>
                {
                    CreateEffectDataWith(tracker1),
                    CreateEffectDataWith(tracker2),
                }
            };

            behavior.Execute(new TestBehaviorContext());

            Assert.AreEqual(1, tracker1.ApplyCount);
            Assert.AreEqual(1, tracker2.ApplyCount);
        }

        [Test]
        public void Execute_ShortCircuitsOnFalse()
        {
            var failTracker = new EffectTracker { ReturnValue = false };
            var secondTracker = new EffectTracker();
            var behavior = new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData>
                {
                    CreateEffectDataWith(failTracker),
                    CreateEffectDataWith(secondTracker),
                }
            };

            behavior.Execute(new TestBehaviorContext());

            Assert.AreEqual(1, failTracker.ApplyCount);
            Assert.AreEqual(0, secondTracker.ApplyCount);
        }

        [Test]
        public void Execute_ClearsStoredValues()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData>()
            };

            behavior.SetBehaviorValue(
                Rollgeon.Entities.Behaviors.BehaviorValueKey.FloatingDamage,
                new Rollgeon.Entities.Behaviors.FloatingNumberBehaviorValue { Value = 42 });

            behavior.Execute(new TestBehaviorContext());

            Assert.IsFalse(behavior.TryGetBehaviorValues<Rollgeon.Entities.Behaviors.FloatingNumberBehaviorValue>(
                Rollgeon.Entities.Behaviors.BehaviorValueKey.FloatingDamage, out _));
        }

        [Test]
        public void Execute_PassesDiceAndComboInContext()
        {
            var capturer = new ContextCapturingEffect();
            var behavior = new HeroActionBehavior
            {
                ActionName = "Test",
                Effects = new List<EffectData>
                {
                    CreateEffectDataWith(capturer),
                }
            };

            var dice = new int[] { 1, 2, 3, 4, 5 };
            var comboResult = ComboDetectionResult.Match(15, 5);
            var heroCtx = new HeroBehaviorContext
            {
                DiceResult = dice,
                MatchedComboResult = comboResult,
                TargetGuid = Guid.NewGuid(),
            };

            behavior.Execute(heroCtx);

            Assert.IsNotNull(capturer.CapturedContext);
            Assert.AreSame(dice, capturer.CapturedContext.DiceResult);
            Assert.IsTrue(capturer.CapturedContext.ComboResult.HasValue);
            Assert.AreEqual(15, capturer.CapturedContext.ComboResult.Value.BaseDamage);
            Assert.AreEqual(heroCtx.TargetGuid, capturer.CapturedContext.TargetGuid);
        }

        [Test]
        public void ShouldShow_EmptyConditions_ReturnsTrue()
        {
            var behavior = new HeroActionBehavior { ShowConditions = new List<BasePreCondition>() };
            Assert.IsTrue(behavior.ShouldShow(new PreConditionContext()));
        }

        [Test]
        public void ShouldShow_NullConditions_ReturnsTrue()
        {
            var behavior = new HeroActionBehavior { ShowConditions = null };
            Assert.IsTrue(behavior.ShouldShow(new PreConditionContext()));
        }

        [Test]
        public void ShouldShow_PassingCondition_ReturnsTrue()
        {
            var behavior = new HeroActionBehavior
            {
                ShowConditions = new List<BasePreCondition> { new AlwaysTruePC() }
            };
            Assert.IsTrue(behavior.ShouldShow(new PreConditionContext()));
        }

        [Test]
        public void ShouldShow_FailingCondition_ReturnsFalse()
        {
            var behavior = new HeroActionBehavior
            {
                ShowConditions = new List<BasePreCondition> { new AlwaysFalsePC() }
            };
            Assert.IsFalse(behavior.ShouldShow(new PreConditionContext()));
        }

        [Test]
        public void BehaviorName_ReturnsActionName()
        {
            var behavior = new HeroActionBehavior { ActionName = "My Attack" };
            Assert.AreEqual("My Attack", behavior.BehaviorName);
        }

        [Test]
        public void Execute_NullEffects_DoesNotThrow()
        {
            var behavior = new HeroActionBehavior { Effects = null };
            Assert.DoesNotThrow(() => behavior.Execute(new TestBehaviorContext()));
        }

        [Test]
        public void Execute_EmptyEffects_DoesNotThrow()
        {
            var behavior = new HeroActionBehavior { Effects = new List<EffectData>() };
            Assert.DoesNotThrow(() => behavior.Execute(new TestBehaviorContext()));
        }

        // ---- Test helpers ---------------------------------------------------

        private static EffectData CreateEffectDataWith(IEffect effect)
        {
            return new EffectData
            {
                Effects = new List<IEffect> { effect },
                PreConditions = new List<BasePreCondition>(),
            };
        }

        private class EffectTracker : IEffect
        {
            public int ApplyCount;
            public bool ReturnValue = true;

            public string GetEffectName() => "EffectTracker";
            public Effects.Selection.SelectionSettings GetSelection() => new Effects.Selection.SelectionSettings();
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context)
            {
                ApplyCount++;
                context.lastResult = ReturnValue;
                return ReturnValue;
            }
        }

        private class ContextCapturingEffect : IEffect
        {
            public EffectContext CapturedContext;

            public string GetEffectName() => "ContextCapturingEffect";
            public Effects.Selection.SelectionSettings GetSelection() => new Effects.Selection.SelectionSettings();
            public bool HasSelectionRequirement() => false;
            public bool RequiresSelectionAt(Effects.Selection.SelectionTiming timing) => false;
            public bool ValidateSelection(Effects.Selection.TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context)
            {
                CapturedContext = context;
                return true;
            }
        }

        [Serializable]
        private class AlwaysTruePC : BasePreCondition
        {
            public override string ConditionName => "AlwaysTrue";
            public override bool Evaluate(PreConditionContext context) => true;
        }

        [Serializable]
        private class AlwaysFalsePC : BasePreCondition
        {
            public override string ConditionName => "AlwaysFalse";
            public override bool Evaluate(PreConditionContext context) => false;
        }

        private class TestBehaviorContext : BehaviorContext { }
    }

    [TestFixture]
    public class TurnManagerHeroBehaviorTests
    {
        private TurnManager _tm;
        private FakeEnergyService _energy;
        private Guid _actor;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeEnergyService();
            _actor = Guid.NewGuid();
            _energy.Current[_actor] = 4;

            _tm = new TurnManager();
            _tm.ConfigureForTests(_energy, actions: null, ruleset: null);
        }

        [TearDown]
        public void Teardown()
        {
            _tm?.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void CanExecute_NullBehavior_ReturnsFalse()
        {
            Assert.IsFalse(_tm.CanExecute((HeroActionBehavior)null, _actor, out var reason));
            Assert.IsNotNull(reason);
        }

        [Test]
        public void CanExecute_EnoughEnergy_ReturnsTrue()
        {
            var behavior = new HeroActionBehavior { ActionName = "test", EnergyCost = 1 };
            Assert.IsTrue(_tm.CanExecute(behavior, _actor, out _));
        }

        [Test]
        public void CanExecute_NotEnoughEnergy_ReturnsFalse()
        {
            var behavior = new HeroActionBehavior { ActionName = "test", EnergyCost = 10 };
            Assert.IsFalse(_tm.CanExecute(behavior, _actor, out var reason));
            StringAssert.Contains("energy", reason.ToLowerInvariant());
        }

        [Test]
        public void TryExecute_SpendsEnergy()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "attack",
                EnergyCost = 2,
                Effects = new List<EffectData>(),
            };

            _tm.TryExecute(behavior, _actor, new TestBehaviorContext());

            Assert.AreEqual(2, _energy.Current[_actor]);
        }

        [Test]
        public void TryExecute_BlockOnRepeat_PreventsSecondExecution()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "special",
                EnergyCost = 1,
                BlockOnRepeat = true,
                Effects = new List<EffectData>(),
            };

            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
            Assert.IsFalse(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
        }

        [Test]
        public void TryExecute_AllowsRepeat_WhenBlockOnRepeatFalse()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "movement",
                EnergyCost = 1,
                BlockOnRepeat = false,
                Effects = new List<EffectData>(),
            };

            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
        }

        [Test]
        public void TryExecute_OnTurnStarted_ClearsRepeatTracking()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "blocked",
                EnergyCost = 0,
                BlockOnRepeat = true,
                Effects = new List<EffectData>(),
            };

            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
            Assert.IsFalse(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));

            EventManager.Trigger(EventName.OnTurnStarted, _actor);
            _energy.Current[_actor] = 4;

            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
        }

        private class TestBehaviorContext : BehaviorContext { }

        private sealed class FakeEnergyService : IEnergyService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
            public int MaxPerEntity = 4;

            public void InitializeForEntity(Guid entityId) => Current[entityId] = MaxPerEntity;
            public bool SpendEnergy(Guid entityId, int cost)
            {
                if (cost < 0) return false;
                if (!Current.TryGetValue(entityId, out var have)) return false;
                if (cost > have) return false;
                Current[entityId] = have - cost;
                return true;
            }
            public void RegenerateAtTurnEnd(Guid entityId) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => MaxPerEntity;
        }
    }
}
