using System;
using Rollgeon.Dice;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma escudo cuando el dado carrier saca su cara máxima y participa en un
    /// combo. Hook: <c>OnComboMatched</c>. Encantamiento "Fortaleza".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddShieldOnMaxFace : IOnComboMatchedTrigger
    {
        [Title("Shield Bonus")]
        [InfoBox("Cuánto escudo se suma cuando el dado muestra su cara máxima en un combo.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            int maxFace = ctx.Slot.Type.MaxFace();
            if (face != maxFace) return;

            int amount = Bonus != null ? Bonus.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusShield += amount;
        }
    }
}