using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma oro al jugador cuando el roll resuelve. Hook: <c>OnRollResolved</c>.
    /// Cubre "Suma oro: Si el dado NO es parte del combo te suma X oro por tirada"
    /// del GDD. La MVP simplifica "NO parte del combo" como "no matcheó ningún
    /// combo" via <see cref="OnlyIfNoComboMatched"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnRoll : IOnRollResolvedTrigger
    {
        [Title("Amount")]
        [InfoBox("Cuánto oro se le suma al jugador. ReadConstantInt para fijo; " +
                 "ReadDiceFace(BagSlotIndex=X) para que el dado X dicte el valor; etc.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        [Title("Condition")]
        [Tooltip("Si está activo, el gold solo se suma cuando NO matcheó ningún combo. " +
                 "GDD: 'Si el dado NO es parte del combo te suma X oro por tirada'.")]
        public bool OnlyIfNoComboMatched = true;

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null || ctx.Effect == null) return;
            if (OnlyIfNoComboMatched && ctx.Effect.ComboResult.HasValue && ctx.Effect.ComboResult.Value.IsMatch)
                return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}
