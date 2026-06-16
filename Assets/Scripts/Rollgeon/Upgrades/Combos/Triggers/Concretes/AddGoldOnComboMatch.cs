using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Triggers.Concretes
{
    /// <summary>
    /// Cubre el ejemplo del usuario: "cada vez que sale escalera ganás 3 de oro".
    /// Hook: <see cref="IOnComboPassiveMatchedTrigger"/>. El amount va via reader
    /// para que el designer pueda fijar literal o escalar via combo counter, etc.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnComboMatch : IOnComboPassiveMatchedTrigger
    {
        [Title("Gold Amount")]
        [InfoBox("Cuánto oro se le suma al jugador cuando el combo target matchea. " +
                 "ReadConstantInt(3) reproduce 'gana 3 oro'. " +
                 "ReadComboCounter('combo.par') escala con la run.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        public void OnComboMatched(ComboPassiveContext ctx)
        {
            if (ctx?.Scratch == null) return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}
