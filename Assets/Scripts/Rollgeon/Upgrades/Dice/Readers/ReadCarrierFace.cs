using System;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Cara que sacó el dado CARRIER del encantamiento en el roll actual — versión
    /// carrier-aware de <see cref="ReadDiceFace"/>: el índice sale del
    /// <see cref="ScratchTriggerContext"/> del dispatch, no de un campo hardcodeado.
    /// Reemplaza el "suma tu propia cara" de ResonantDoubleCount legacy.
    /// </summary>
    /// <remarks>Devuelve 0 sin trigger context, sin caras o con índice fuera de rango.</remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCarrierFace : EffectIntReader
    {
        public override int Read(EffectContext context)
        {
            if (context?.DiceResult == null) return 0;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return 0;

            int idx = trig.Slot.Value.BagSlotIndex;
            if (idx < 0 || idx >= context.DiceResult.Count) return 0;
            return context.DiceResult[idx];
        }
    }
}
