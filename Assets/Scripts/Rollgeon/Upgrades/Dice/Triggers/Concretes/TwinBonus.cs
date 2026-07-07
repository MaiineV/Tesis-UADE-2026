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
    /// <remarks>
    /// <b>Excepción documentada (Spec de Daño v2, D3).</b> Este trigger multiplica el daño de
    /// combo directamente vía <c>scratch.ComboDamageMultiplier</c> — es exactamente el patrón
    /// que el spec marca como "señal de alarma" si se generaliza. Se preserva porque ya está
    /// autorado como contenido jugado/balanceado; compone solo sobre el término
    /// <c>daño_combo_base</c> en <see cref="Rollgeon.Combat.Damage.PlayerComboDamage.Resolve"/>,
    /// nunca sobre <c>dmg_base_PJ</c>/<c>bonos_PJ</c>/<c>bono_combo</c>. No copiar este patrón
    /// para encantamientos nuevos — un ítem nuevo que quiera sumar debe usar
    /// <c>BonusComboDamage</c> (aditivo), no <c>ComboDamageMultiplier</c>.
    /// </remarks>
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