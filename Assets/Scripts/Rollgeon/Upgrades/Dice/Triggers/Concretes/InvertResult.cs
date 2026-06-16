using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Invierte el resultado del dado: resultado = maxFace + 1 - face.
    /// Hook: <c>OnRollResolved</c>. Encantamiento "Invertido" (negativo).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Aproximación MVP.</b> El scratch no soporta modificación per-die del
    /// resultado todavía — se calcula el delta entre el valor invertido y el
    /// original y se aplica como bonus/penalidad al combo. Phase 4 agrega
    /// per-die result modification y este trigger se actualiza para modificar
    /// <c>DiceResult[idx]</c> directamente.
    /// </para>
    /// <para>
    /// Ejemplo: D6 saca 2 -> invertido = 5, delta = +3.
    /// D6 saca 5 -> invertido = 2, delta = -3.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class InvertResult : IOnRollResolvedTrigger
    {
        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            int maxFace = ctx.Slot.Type.MaxFace();
            int inverted = maxFace + 1 - face;
            // Aproximación: aplica el delta como bonus/penalidad al combo hasta
            // que Phase 4 soporte modificación per-die del resultado.
            int delta = inverted - face;
            ctx.Scratch.BonusComboDamage += delta;
        }
    }
}