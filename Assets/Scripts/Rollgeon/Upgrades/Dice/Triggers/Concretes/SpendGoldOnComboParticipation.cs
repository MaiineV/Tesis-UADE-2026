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
    /// "Evil": consume oro cada vez que el dado participa en un combo. Si no hay
    /// suficiente oro, bloquea el daño del combo. Hook: <c>OnComboMatched</c>.
    /// Encantamiento "Sediento" (negativo).
    /// </summary>
    /// <remarks>
    /// El costo se aplica via <c>scratch.BonusGold -= cost</c>. Si el oro efectivo
    /// (balance actual + scratch acumulado) no alcanza, setea
    /// <see cref="EnchantmentScratch.BlockComboDamage"/> para anular el combo.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class SpendGoldOnComboParticipation : IOnComboMatchedTrigger
    {
        [Title("Cost")]
        [InfoBox("Cuánto oro se consume cada vez que el dado participa en un combo. " +
                 "Si no alcanza, el combo no hace daño.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Cost;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;

            int cost = Cost != null ? Cost.Read(ctx.Effect) : 0;
            if (cost <= 0) return;

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return;

            // Considera tanto el gold actual como el scratch ya acumulado por triggers previos.
            int effectiveGold = economy.CurrentGold + ctx.Scratch.BonusGold;
            if (effectiveGold < cost)
            {
                ctx.Scratch.BlockComboDamage = true;
                return;
            }

            ctx.Scratch.BonusGold -= cost;
        }
    }
}