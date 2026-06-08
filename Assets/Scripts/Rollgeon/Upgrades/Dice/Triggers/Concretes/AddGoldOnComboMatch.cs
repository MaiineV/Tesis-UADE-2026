using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma oro al jugador cuando el dado carrier participa en cualquier combo.
    /// Hook: <c>OnComboMatched</c>. Encantamiento "Codicioso".
    /// </summary>
    /// <remarks>
    /// Distinto de <c>Rollgeon.Upgrades.Combos.Triggers.Concretes.AddGoldOnComboMatch</c>
    /// que es un passive de combo — este vive en el dado y se activa cuando el dado
    /// es parte de un combo cualquiera.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnComboMatch : IOnComboMatchedTrigger
    {
        [Title("Gold Amount")]
        [InfoBox("Cuánto oro se le suma al jugador cuando este dado participa en un combo. " +
                 "ReadConstantInt para fijo; ReadDiceFace para escalar con la cara; etc.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}