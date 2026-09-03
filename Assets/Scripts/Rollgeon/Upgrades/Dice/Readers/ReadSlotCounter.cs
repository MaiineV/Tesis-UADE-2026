using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Lee el counter per-slot del dado CARRIER (el que alimenta <c>EffSlotCounter</c>)
    /// y lo escala: <c>Offset + counter × Multiplier</c>. Es el reader de "Racha"
    /// (+3 por participación consecutiva) y de cualquier encantamiento que acumule
    /// estado propio por dado.
    /// </summary>
    /// <remarks>Devuelve 0 sin trigger context con Slot o sin runtime registrado.</remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadSlotCounter : EffectIntReader
    {
        [Tooltip("Clave del counter per-slot — la misma que usa el EffSlotCounter que lo alimenta.")]
        public string Key = "counter";

        [Tooltip("Cuánto vale cada unidad del counter.")]
        public int Multiplier = 1;

        [Tooltip("Se suma al final, independiente del counter.")]
        public int Offset;

        [Tooltip("Tope del counter antes de escalar. 0 = sin tope.")]
        [MinValue(0)]
        public int MaxCount;

        public override int Read(EffectContext context)
        {
            if (context == null) return 0;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return 0;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var runtime) || runtime == null)
                return 0;

            int count = runtime.GetCounter(trig.Slot.Value, Key);
            if (MaxCount > 0 && count > MaxCount) count = MaxCount;
            return Offset + count * Multiplier;
        }
    }
}
