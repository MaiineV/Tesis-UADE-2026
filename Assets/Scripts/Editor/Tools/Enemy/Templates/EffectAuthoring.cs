using System;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Attributes;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Feedback;

namespace Rollgeon.Editor.Tools.Enemy.Templates
{
    /// <summary>
    /// Autoría desde código de los efectos cuyos campos de daño/energía son privados
    /// (<c>_damageSource</c>, <c>_reader</c>, <c>_steps</c>…). El runtime los expone solo al
    /// inspector; para armar plantillas hace falta setearlos por reflexión. Los nombres viven en
    /// las listas públicas de abajo y un test guardián falla si el runtime los renombra.
    /// </summary>
    public static class EffectAuthoring
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        public static readonly string[] DealDamageFields = { "_damageSource", "_reader", "_readerMultiplier", "_attackKind" };
        public static readonly string[] ModifyIntFields = { "_amountSource", "_baseAmount", "_reader", "_clampHealthToMax" };
        public static readonly string[] PlaySequenceFields = { "_steps" };

        /// <summary>Daño leído de un stat del atacante (ATK por defecto), como los enemigos autorados a mano.</summary>
        public static EffDealDamage DealDamageFromStat(StatType stat = StatType.Attack, float multiplier = 1f,
                                                       AttackKind kind = AttackKind.BasicAttack)
        {
            var e = new EffDealDamage();
            Set(e, "_damageSource", DamageSource.FromReader);
            Set(e, "_reader", new ReadEntityStat { Entity = ReaderEntitySource.Source, Stat = stat, UseModified = true });
            Set(e, "_readerMultiplier", multiplier);
            Set(e, "_attackKind", kind);
            return e;
        }

        public static EffModifyIntAttribute ModifyStat(StatType stat, IntOperation op, int amount)
        {
            var e = new EffModifyIntAttribute { TargetStat = stat, Operation = op };
            Set(e, "_amountSource", DamageSource.Constant);
            Set(e, "_baseAmount", amount);
            return e;
        }

        /// <summary>Cura leída de un stat del que cura (HealStrength), clampeada al máximo de vida.</summary>
        public static EffModifyIntAttribute HealFromStat(StatType stat = StatType.HealStrength)
        {
            var e = new EffModifyIntAttribute { TargetStat = StatType.Health, Operation = IntOperation.Add };
            Set(e, "_amountSource", DamageSource.FromReader);
            Set(e, "_reader", new ReadEntityStat { Entity = ReaderEntitySource.Source, Stat = stat, UseModified = true });
            Set(e, "_clampHealthToMax", true);
            return e;
        }

        public static EffPlaySequence Sequence(params FeedbackSequenceStep[] steps)
        {
            var e = new EffPlaySequence();
            Set(e, "_steps", new List<FeedbackSequenceStep>(steps));
            return e;
        }

        public static FeedbackSequenceStep Step(string feedbackRefId, StepEndMode end = StepEndMode.OnDuration,
                                                string endEventKey = null, float durationOverride = 0f)
        {
            return new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = feedbackRefId,
                EndMode = end,
                EndOnEventKey = endEventKey,
                DurationOverride = durationOverride,
            };
        }

        public static object Get(object target, string field) => Field(target.GetType(), field).GetValue(target);

        static void Set(object target, string field, object value) => Field(target.GetType(), field).SetValue(target, value);

        static FieldInfo Field(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Private);
                if (f != null) return f;
            }
            throw new MissingFieldException(type.Name, name);
        }
    }
}
