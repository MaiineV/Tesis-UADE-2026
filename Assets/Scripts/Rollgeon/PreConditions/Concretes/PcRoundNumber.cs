using System;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara el round actual contra <see cref="Value"/> según <see cref="Mode"/>.
    /// Reemplaza a <c>AICond_RoundNumber</c>. El round llega via
    /// <see cref="PreConditionContext.RoundIndex"/> (lo popula el bridge AI). Si el
    /// caller no provee round (ej. flujo héroe), evalúa <c>true</c> — semántica
    /// permisiva, igual que la regla "lista vacía = pasa" de PC.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcRoundNumber : BasePreCondition
    {
        public enum CompareMode
        {
            Equal,
            GreaterOrEqual,
            LessOrEqual,
            Multiple,
        }

        public CompareMode Mode = CompareMode.Multiple;

        [MinValue(1)]
        public int Value = 3;

        public override string ConditionName => $"Round {Mode} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return true;
            if (!context.RoundIndex.HasValue) return true; // permissive: no round info → no veto.
            int r = context.RoundIndex.Value;
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
