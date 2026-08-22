using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Phase;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    [TestFixture]
    public class ClassHeroSOPhaseBehaviorTests
    {
        [TearDown]
        public void Teardown()
        {
            ServiceLocator.Clear();
        }

        [Test]
        public void ClassHeroSO_PhaseBehaviors_DefaultEmpty()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                Assert.IsNotNull(so.PhaseBehaviors);
                Assert.AreEqual(0, so.PhaseBehaviors.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void ResolveBaseBehavior_WithPhaseBehavior_ReturnsOverride()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var explorationMove = new HeroActionBehavior
                {
                    ActionName = "ExploreMove",
                    IsBaseBehavior = true,
                    Slot = HeroBehaviorSlot.Movement,
                    AllowedPhases = GamePhaseMask.Exploration,
                };
                so.PhaseBehaviors.Add(explorationMove);

                var result = so.ResolveBaseBehavior(HeroBehaviorSlot.Movement, GamePhase.Exploration);
                Assert.AreSame(explorationMove, result);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void ResolveBaseBehavior_NoPhaseBehavior_ReturnsNull()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var result = so.ResolveBaseBehavior(HeroBehaviorSlot.Movement, GamePhase.Combat);
                Assert.IsNull(result);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void ResolveBaseBehavior_PhaseMismatch_ReturnsNull()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var explorationOnly = new HeroActionBehavior
                {
                    ActionName = "ExploreMove",
                    IsBaseBehavior = true,
                    Slot = HeroBehaviorSlot.Movement,
                    AllowedPhases = GamePhaseMask.Exploration,
                };
                so.PhaseBehaviors.Add(explorationOnly);

                var result = so.ResolveBaseBehavior(HeroBehaviorSlot.Movement, GamePhase.Combat);
                Assert.IsNull(result);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_Combat_ReturnsAllBaseSlots()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var movement     = MakeBase(HeroBehaviorSlot.Movement,      GamePhaseMask.All);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.All);
                so.PhaseBehaviors.AddRange(new[] { movement, baseAttack, specialAttack, healing });

                var result = so.GetBehaviorsForPhase(GamePhase.Combat);
                Assert.AreEqual(4, result.Count);
                Assert.AreSame(movement,      result[0]);
                Assert.AreSame(baseAttack,    result[1]);
                Assert.AreSame(specialAttack, result[2]);
                Assert.AreSame(healing,       result[3]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_Combat_WithAllSixSlots_ReturnsDefenseAtIndexFive()
        {
            // El HUD indexa GetBehaviorsForPhase(Combat) con el índice del array de
            // chips — Defense (slot 5) DEBE salir en la posición 5 con todos los
            // slots base presentes, o el click ejecutaría otro behavior.
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var movement     = MakeBase(HeroBehaviorSlot.Movement,      GamePhaseMask.All);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.All);
                var forceDoor    = MakeBase(HeroBehaviorSlot.ForceDoor,     GamePhaseMask.Combat);
                var defense      = MakeBase(HeroBehaviorSlot.Defense,       GamePhaseMask.Combat);
                // Orden de inserción distinto al del enum a propósito: el orden de
                // salida lo dicta el enum, no la lista.
                so.PhaseBehaviors.AddRange(new[] { defense, movement, baseAttack, specialAttack, healing, forceDoor });

                var result = so.GetBehaviorsForPhase(GamePhase.Combat);

                Assert.AreEqual(6, result.Count);
                Assert.AreSame(movement,      result[0]);
                Assert.AreSame(baseAttack,    result[1]);
                Assert.AreSame(specialAttack, result[2]);
                Assert.AreSame(healing,       result[3]);
                Assert.AreSame(forceDoor,     result[4]);
                Assert.AreSame(defense,       result[5]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_Exploration_ExcludesCombatOnly()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var movement     = MakeBase(HeroBehaviorSlot.Movement,      GamePhaseMask.All);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.All);
                so.PhaseBehaviors.AddRange(new[] { movement, baseAttack, specialAttack, healing });

                var result = so.GetBehaviorsForPhase(GamePhase.Exploration);
                Assert.AreEqual(2, result.Count);
                Assert.AreSame(movement, result[0]);
                Assert.AreSame(healing,  result[1]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_Exploration_SelectsPhaseMatchingBase()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var exploreMove  = new HeroActionBehavior
                {
                    ActionName    = "FreeMove",
                    IsBaseBehavior = true,
                    Slot          = HeroBehaviorSlot.Movement,
                    AllowedPhases = GamePhaseMask.Exploration,
                };
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.All);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                so.PhaseBehaviors.AddRange(new[] { exploreMove, healing, baseAttack, specialAttack });

                var result = so.GetBehaviorsForPhase(GamePhase.Exploration);
                Assert.AreEqual(2, result.Count);
                Assert.AreSame(exploreMove, result[0]);
                Assert.AreSame(healing,     result[1]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_IncludesCustomNonBaseBehaviors()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var movement     = MakeBase(HeroBehaviorSlot.Movement,      GamePhaseMask.Exploration);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.Combat);
                var custom       = new HeroActionBehavior
                {
                    ActionName    = "Force Door",
                    IsBaseBehavior = false,
                    AllowedPhases = GamePhaseMask.Exploration,
                };
                so.PhaseBehaviors.AddRange(new[] { movement, baseAttack, specialAttack, healing, custom });

                var result = so.GetBehaviorsForPhase(GamePhase.Exploration);
                Assert.AreEqual(2, result.Count);
                Assert.AreSame(movement, result[0]);
                Assert.AreSame(custom,   result[1]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void GetBehaviorsForPhase_CustomBehavior_FilteredByPhase()
        {
            var so = ScriptableObject.CreateInstance<ClassHeroSO>();
            try
            {
                var movement     = MakeBase(HeroBehaviorSlot.Movement,      GamePhaseMask.Combat);
                var baseAttack   = MakeBase(HeroBehaviorSlot.BaseAttack,    GamePhaseMask.Combat);
                var specialAttack= MakeBase(HeroBehaviorSlot.SpecialAttack, GamePhaseMask.Combat);
                var healing      = MakeBase(HeroBehaviorSlot.Healing,       GamePhaseMask.Combat);
                var combatCustom = new HeroActionBehavior
                {
                    ActionName    = "Taunt",
                    IsBaseBehavior = false,
                    AllowedPhases = GamePhaseMask.Combat,
                };
                so.PhaseBehaviors.AddRange(new[] { movement, baseAttack, specialAttack, healing, combatCustom });

                var explorationResult = so.GetBehaviorsForPhase(GamePhase.Exploration);
                Assert.AreEqual(0, explorationResult.Count);

                var combatResult = so.GetBehaviorsForPhase(GamePhase.Combat);
                Assert.AreEqual(5, combatResult.Count);
                Assert.AreSame(combatCustom, combatResult[4]);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        private static HeroActionBehavior MakeBase(HeroBehaviorSlot slot, GamePhaseMask phases)
            => new HeroActionBehavior { IsBaseBehavior = true, Slot = slot, AllowedPhases = phases };
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
        private FakeRollPoolService _energy;
        private Guid _actor;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeRollPoolService();
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
        public void CanExecute_PoolWithRolls_ReturnsTrue()
        {
            var behavior = new HeroActionBehavior { ActionName = "test" };
            Assert.IsTrue(_tm.CanExecute(behavior, _actor, out _));
        }

        [Test]
        public void CanExecute_EmptyPool_ReturnsFalse()
        {
            var behavior = new HeroActionBehavior { ActionName = "test" };
            _energy.Current[_actor] = 0;
            Assert.IsFalse(_tm.CanExecute(behavior, _actor, out var reason));
            StringAssert.Contains("rolls", reason.ToLowerInvariant());
        }

        [Test]
        public void TryExecute_SpendsOneRoll()
        {
            var behavior = new HeroActionBehavior
            {
                ActionName = "attack",
                Effects = new List<EffectData>(),
            };

            _tm.TryExecute(behavior, _actor, new TestBehaviorContext());

            Assert.AreEqual(3, _energy.Current[_actor]);
        }

        [Test]
        public void TryExecute_SameBehavior_RepeatsWhileRollsRemain()
        {
            // Sin limite de acciones por turno: el unico presupuesto es el pool.
            var behavior = new HeroActionBehavior
            {
                ActionName = "special",
                Effects = new List<EffectData>(),
            };
            _energy.Current[_actor] = 2;

            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
            Assert.IsTrue(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()));
            Assert.IsFalse(_tm.TryExecute(behavior, _actor, new TestBehaviorContext()),
                "Con el pool en 0 la accion deja de poder ejecutarse.");
            Assert.AreEqual(0, _energy.Current[_actor]);
        }

        private class TestBehaviorContext : BehaviorContext { }

        private sealed class FakeRollPoolService : Rollgeon.Combat.Rolls.IRollPoolService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
            public int Cap = 15;

            public bool IsCombatActive => true;
            public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;
            public bool TrySpendRolls(Guid entityId, int count)
            {
                if (count < 0) return false;
                if (count == 0) return true;
                if (!Current.TryGetValue(entityId, out var have)) return false;
                if (count > have) return false;
                Current[entityId] = have - count;
                return true;
            }
            public int Drain(Guid entityId, int amount)
            {
                if (amount <= 0 || !Current.TryGetValue(entityId, out var have)) return 0;
                int drained = Math.Min(amount, have);
                Current[entityId] = have - drained;
                return drained;
            }
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => Cap;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddPerTurnGrantBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value)
                => Current[entityId] = Math.Clamp(value, 0, Cap);
        }
    }
}
