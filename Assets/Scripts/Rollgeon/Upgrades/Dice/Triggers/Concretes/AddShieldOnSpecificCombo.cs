using System;
using System.Collections.Generic;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma escudo cuando matchea un combo específico. Hook: <c>OnComboMatched</c>.
    /// Cubre "Escudo: Si se utiliza en un combo de escudo da X mas de escudo" del GDD.
    /// El diseñador define qué <see cref="TargetComboIds"/> cuentan como "combos de escudo".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddShieldOnSpecificCombo : IOnComboMatchedTrigger
    {
        [Title("Shield Bonus")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        [Title("Target Combos")]
        [InfoBox("IDs de combos que cuentan como 'combos de escudo' (ej. 'combo.full_house'). " +
                 "Empty = aplica a cualquier combo (degenerado, evitar).")]
        public List<string> TargetComboIds = new List<string>();

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (TargetComboIds != null && TargetComboIds.Count > 0)
            {
                if (string.IsNullOrEmpty(ctx.ComboId)) return;
                if (!TargetComboIds.Contains(ctx.ComboId)) return;
            }
            int amount = Bonus != null ? Bonus.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusShield += amount;
        }
    }
}
