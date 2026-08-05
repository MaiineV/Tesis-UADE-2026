using System;
using Patterns;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.PreConditions
{
    /// <summary>
    /// Compara el counter per-slot <c>(slot del carrier, Key)</c> de
    /// <see cref="IDiceEnchantmentRuntime"/> contra <see cref="Value"/>. Gating de
    /// composiciones stateful ("si llegó a N turnos sin uso → explota").
    /// </summary>
    /// <remarks>
    /// Sin trigger context con Slot o sin runtime devuelve <c>false</c> — el gate no
    /// evaluable no habilita efectos (mismo criterio que <c>PcCarrierFace</c>).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcSlotCounterCompare : BasePreCondition
    {
        [Tooltip("Clave del counter per-slot — la misma que usa el EffSlotCounter que lo alimenta.")]
        public string Key = "counter";

        public IntComparison Comparison = IntComparison.GreaterOrEqual;

        public int Value = 3;

        public override string ConditionName => $"Counter '{Key}' {Comparison} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            var eff = context?.Effect;
            if (eff == null) return false;
            if (!eff.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return false;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var runtime) || runtime == null)
                return false;

            int current = runtime.GetCounter(trig.Slot.Value, Key);
            return Apply(current, Comparison, Value);
        }

        private static bool Apply(int a, IntComparison op, int b) => op switch
        {
            IntComparison.Equal          => a == b,
            IntComparison.NotEqual       => a != b,
            IntComparison.Less           => a <  b,
            IntComparison.LessOrEqual    => a <= b,
            IntComparison.Greater        => a >  b,
            IntComparison.GreaterOrEqual => a >= b,
            _                            => false,
        };
    }
}
