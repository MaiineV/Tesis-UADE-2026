using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Si este dado muestra el mismo numero que otro dado en la misma tirada,
    /// ambos cuentan x1.5 para el combo. Hook: <c>OnComboMatched</c>.
    /// Encantamiento "Gemelo".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class TwinBonus : IOnComboMatchedTrigger
    {
        [Title("Twin Multiplier")]
        [InfoBox("Multiplicador al dano del combo cuando este dado comparte cara " +
                 "con otro dado de la tirada. Default: 1.5x.")]
        [MinValue(1f)]
        public float BonusMultiplier = 1.5f;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int carrierFace = ctx.Effect.DiceResult[idx];
            bool hasTwin = false;

            for (int i = 0; i < ctx.Effect.DiceResult.Count; i++)
            {
                if (i != idx && ctx.Effect.DiceResult[i] == carrierFace)
                {
                    hasTwin = true;
                    break;
                }
            }

            if (hasTwin)
            {
                ctx.Scratch.ComboDamageMultiplier *= BonusMultiplier;
            }
        }
    }
}