using System;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>Transformación de roll cuya delta calcula <see cref="ReadCarrierRollDelta"/>.</summary>
    public enum CarrierRollDeltaOp
    {
        /// <summary>Invertido: delta = (max+1-cara) - cara. D6 con 2 → +3; con 5 → -3.</summary>
        Invert,

        /// <summary>Afilado: la cara no baja de ceil(max/2); delta = max(0, mínimo - cara).</summary>
        ClampMinToHalfMax,

        /// <summary>Volátil legacy: cara máxima duplica (+cara); cara 1 anula (-1); resto 0.</summary>
        DoubleMaxZeroMin,

        // APPEND-ONLY: los SOs serializan el int del enum.

        /// <summary>"El dado no suma": delta = -cara. El dado sigue contando para matchear el combo.</summary>
        Exclude,

        /// <summary>"El dado vale el doble": delta = +cara.</summary>
        Double,

        /// <summary>Volátil GDD: cara máxima duplica (+cara); el resto vale ceil(cara/2) (delta = ceil(cara/2) - cara).</summary>
        DoubleMaxHalveRest,

        /// <summary>Enfiestado: impar triplica (+2·cara); par aporta 0 (-cara).</summary>
        TripleOddZeroEven,
    }

    /// <summary>
    /// Delta entre la cara transformada y la original del dado CARRIER. Centraliza la
    /// matemática de los "mutadores de roll" legacy (Invertido / Afilado / Volátil) que
    /// hoy son aproximaciones MVP vía <c>BonusComboDamage</c> — cuando exista mutación
    /// per-die real, los assets cambian este reader por el efecto nuevo sin tocar infra.
    /// </summary>
    /// <remarks>Devuelve 0 sin trigger context, sin caras o con índice fuera de rango.</remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCarrierRollDelta : EffectIntReader
    {
        public CarrierRollDeltaOp Op = CarrierRollDeltaOp.Invert;

        public override int Read(EffectContext context)
        {
            if (context?.DiceResult == null) return 0;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return 0;

            var slot = trig.Slot.Value;
            int idx = slot.BagSlotIndex;
            if (idx < 0 || idx >= context.DiceResult.Count) return 0;

            int face = context.DiceResult[idx];
            int maxFace = slot.Type.MaxFace();

            switch (Op)
            {
                case CarrierRollDeltaOp.Invert:
                    return (maxFace + 1 - face) - face;

                case CarrierRollDeltaOp.ClampMinToHalfMax:
                    int minAllowed = (maxFace + 1) / 2;
                    return face < minAllowed ? minAllowed - face : 0;

                case CarrierRollDeltaOp.DoubleMaxZeroMin:
                    if (face == maxFace) return face;
                    if (face == 1) return -1;
                    return 0;

                case CarrierRollDeltaOp.Exclude:
                    return -face;

                case CarrierRollDeltaOp.Double:
                    return face;

                case CarrierRollDeltaOp.DoubleMaxHalveRest:
                    // ceil para que ninguna cara valga 0 (un d20 con 1 sigue aportando 1).
                    return face == maxFace ? face : (face + 1) / 2 - face;

                case CarrierRollDeltaOp.TripleOddZeroEven:
                    return (face % 2) != 0 ? 2 * face : -face;

                default:
                    return 0;
            }
        }
    }
}
