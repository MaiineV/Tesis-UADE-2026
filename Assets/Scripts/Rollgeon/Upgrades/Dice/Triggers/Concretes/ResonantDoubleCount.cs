using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Si 2+ dados muestran el mismo numero en la tirada final, el valor de
    /// este dado cuenta dos veces para el combo. Hook: <c>OnComboMatched</c>.
    /// Encantamiento "Resonante".
    /// </summary>
    /// <remarks>
    /// El bonus se aplica como <c>BonusComboDamage</c> sumando el face value
    /// una segunda vez. Solo se activa si al menos otro dado comparte la cara
    /// con el carrier (matches &gt;= 2 contando al carrier).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ResonantDoubleCount : IOnComboMatchedTrigger
    {
        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int carrierFace = ctx.Effect.DiceResult[idx];

            int matches = 0;
            foreach (var f in ctx.Effect.DiceResult)
            {
                if (f == carrierFace) matches++;
            }

            // carrier + al menos otro dado con la misma cara
            if (matches >= 2)
            {
                ctx.Scratch.BonusComboDamage += carrierFace;
            }
        }
    }
}