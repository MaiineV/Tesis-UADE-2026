using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects;
using Rollgeon.PreConditions;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Units de <see cref="ExecuteEffectsOnEvent"/> — el trigger puente que ejecuta
    /// <see cref="EffectData"/> del pipeline estándar sobre el evento elegido:
    /// filtro por evento, contexto fresco por grupo, target default = source y
    /// gate de preconditions.
    /// </summary>
    [TestFixture]
    public class ExecuteEffectsOnEventTests
    {
        // ---- Stubs -----------------------------------------------------

        private sealed class RecordingEffect : BaseEffect
        {
            public int Applies;
            public EffectContext LastContext;
            public bool ReturnValue = true;

            protected override bool ShowSelection => false;

            public override bool ApplyEffect(EffectContext context)
            {
                Applies++;
                LastContext = context;
                return ReturnValue;
            }
        }

        private sealed class AlwaysFalsePreCondition : BasePreCondition
        {
            public override string ConditionName => "AlwaysFalse (test)";
            public override bool Evaluate(PreConditionContext context) => false;
        }

        private static ComboPassiveContext MakeContext(Guid? source = null, Guid? target = null)
        {
            return new ComboPassiveContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = source ?? Guid.NewGuid(),
                    TargetGuid = target ?? Guid.Empty,
                },
                Scratch = new EnchantmentScratch(),
            };
        }

        private static ExecuteEffectsOnEvent MakeTrigger(ComboPassiveHookEvent hookEvent, params EffectData[] groups)
        {
            return new ExecuteEffectsOnEvent
            {
                Event = hookEvent,
                Effects = new List<EffectData>(groups),
            };
        }

        private static EffectData MakeGroup(params IEffect[] effects)
        {
            return new EffectData { Effects = new List<IEffect>(effects) };
        }

        // ---- Filtro por evento -----------------------------------------

        [Test]
        public void MatchingEvent_ExecutesEffects()
        {
            var effect = new RecordingEffect();
            var trigger = MakeTrigger(ComboPassiveHookEvent.RoomEntered, MakeGroup(effect));

            trigger.OnRoomEntered(MakeContext());

            Assert.AreEqual(1, effect.Applies);
        }

        [Test]
        public void NonMatchingEvent_DoesNotExecute()
        {
            var effect = new RecordingEffect();
            var trigger = MakeTrigger(ComboPassiveHookEvent.RoomEntered, MakeGroup(effect));

            trigger.OnTurnStarted(MakeContext());
            trigger.OnCombatEnd(MakeContext());
            trigger.OnGoldChanged(MakeContext());

            Assert.AreEqual(0, effect.Applies, "Solo el evento configurado debe ejecutar los efectos.");
        }

        // ---- Contexto --------------------------------------------------

        [Test]
        public void TargetDefaultsToSource_WhenEventHasNoTarget()
        {
            var source = Guid.NewGuid();
            var effect = new RecordingEffect();
            var trigger = MakeTrigger(ComboPassiveHookEvent.TurnStarted, MakeGroup(effect));

            trigger.OnTurnStarted(MakeContext(source: source));

            Assert.AreEqual(source, effect.LastContext.SourceGuid);
            Assert.AreEqual(source, effect.LastContext.TargetGuid, "Sin target explícito, self-cast.");
        }

        [Test]
        public void ExplicitTarget_IsPreserved()
        {
            var source = Guid.NewGuid();
            var target = Guid.NewGuid();
            var effect = new RecordingEffect();
            var trigger = MakeTrigger(ComboPassiveHookEvent.DamageResolved, MakeGroup(effect));

            trigger.OnDamageResolved(MakeContext(source: source, target: target));

            Assert.AreEqual(target, effect.LastContext.TargetGuid);
        }

        [Test]
        public void EachGroup_RunsWithFreshContext()
        {
            // El primer grupo corta su cadena (ApplyEffect false) — el segundo grupo
            // debe correr igual: contexto fresco por grupo, sin arrastrar lastResult.
            var failing = new RecordingEffect { ReturnValue = false };
            var second = new RecordingEffect();
            var trigger = MakeTrigger(ComboPassiveHookEvent.RollResolved,
                MakeGroup(failing), MakeGroup(second));

            trigger.OnRollResolved(MakeContext());

            Assert.AreEqual(1, failing.Applies);
            Assert.AreEqual(1, second.Applies, "El cortocircuito de un grupo no debe frenar al siguiente.");
        }

        // ---- Preconditions ---------------------------------------------

        [Test]
        public void FailingPrecondition_BlocksGroup()
        {
            var effect = new RecordingEffect();
            var group = MakeGroup(effect);
            group.PreConditions = new List<BasePreCondition> { new AlwaysFalsePreCondition() };
            var trigger = MakeTrigger(ComboPassiveHookEvent.CombatStart, group);

            trigger.OnCombatStart(MakeContext());

            Assert.AreEqual(0, effect.Applies, "Con precondition en false el grupo no debe ejecutarse.");
        }

        // ---- Robustez --------------------------------------------------

        [Test]
        public void NullEffectContext_NoThrow()
        {
            var trigger = MakeTrigger(ComboPassiveHookEvent.RoomEntered, MakeGroup(new RecordingEffect()));
            var ctx = new ComboPassiveContext { Effect = null };

            Assert.DoesNotThrow(() => trigger.OnRoomEntered(ctx));
        }

        [Test]
        public void EmptyEffectsList_NoThrow()
        {
            var trigger = new ExecuteEffectsOnEvent { Event = ComboPassiveHookEvent.RoomEntered };

            Assert.DoesNotThrow(() => trigger.OnRoomEntered(MakeContext()));
        }
    }
}
