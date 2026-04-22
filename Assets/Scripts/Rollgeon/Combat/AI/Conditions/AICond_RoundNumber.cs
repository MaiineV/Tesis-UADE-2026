using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Conditions
{
    /// <summary>
    /// Condición sobre <see cref="AIContext.RoundIndex"/> (1-based).
    /// </summary>
    /// <remarks>
    /// Patrón típico: Boss que dispara ComboBlock cada 3 rondas →
    /// <c>AICond_RoundNumber{Mode=Multiple, Value=3}</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_RoundNumber : AICondition
    {
        public enum CompareMode
        {
            Equal,
            GreaterOrEqual,
            LessOrEqual,
            Multiple,
        }

        public CompareMode Mode = CompareMode.Multiple;
        [MinValue(1)] public int Value = 3;

        public override string ConditionName => $"Round {Mode} {Value}";

        public override bool Evaluate(AIContext context)
        {
            if (context == null) return false;
            int r = context.RoundIndex;
            switch (Mode)
            {
                case CompareMode.Equal: return r == Value;
                case CompareMode.GreaterOrEqual: return r >= Value;
                case CompareMode.LessOrEqual: return r <= Value;
                case CompareMode.Multiple: return Value > 0 && r > 0 && r % Value == 0;
                default: return false;
            }
        }
    }
}
