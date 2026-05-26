using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Evil": cuando matchea un combo, si el oro actual del jugador es menor a
    /// <see cref="Threshold"/>, anula el daño del combo (lo deja en 0). Hook:
    /// <c>OnComboMatched</c>. Cubre el ejemplo del usuario: "Que si no tengo oro
    /// no hace daño".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class BlockComboIfBelowGold : IOnComboMatchedTrigger
    {
        [Title("Threshold")]
        [InfoBox("Si el gold actual del jugador es menor que este valor, el combo no hace daño. " +
                 "Setear Threshold=1 reproduce el GDD ('no tener nada de oro = no hace daño').")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Threshold;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;

            int threshold = Threshold != null ? Threshold.Read(ctx.Effect) : 1;
            if (threshold <= 0) return; // Sin threshold útil, no aplica.

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return;

            // Considera el scratch ya acumulado — costos previos pueden bajar el gold efectivo.
            int effectiveGold = economy.CurrentGold + ctx.Scratch.BonusGold;
            if (effectiveGold < threshold)
            {
                ctx.Scratch.BlockComboDamage = true;
            }
        }
    }
}
