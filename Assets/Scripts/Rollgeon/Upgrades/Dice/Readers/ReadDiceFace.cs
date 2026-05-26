using System;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Reader que devuelve la cara que sacó un dado específico del bag en el
    /// roll actual via <see cref="EffectContext.DiceResult"/>. Útil para triggers
    /// que escalan con el valor crudo de un dado (ej. "+daño igual a la cara del
    /// D20").
    /// </summary>
    /// <remarks>
    /// Si <paramref name="context"/> o <c>DiceResult</c> son null, o el índice
    /// está fuera de rango, devuelve 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadDiceFace : EffectIntReader
    {
        [Tooltip("Índice del dado dentro del bag (0..4). Lee la cara que salió " +
                 "en EffectContext.DiceResult[BagSlotIndex].")]
        [MinValue(0), MaxValue(4)]
        public int BagSlotIndex;

        public override int Read(EffectContext context)
        {
            if (context?.DiceResult == null) return 0;
            if (BagSlotIndex < 0 || BagSlotIndex >= context.DiceResult.Count) return 0;
            return context.DiceResult[BagSlotIndex];
        }
    }
}
