using System;
using System.Collections.Generic;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma oro cuando el combo es trío o superior (3+ dados iguales).
    /// Hook: <c>OnComboMatched</c>. Encantamiento "Avaro".
    /// </summary>
    /// <remarks>
    /// El diseñador define en <see cref="TrioComboIds"/> qué combos cuentan como
    /// "trío o mejor" — típicamente combo.trio, combo.poker, combo.generala.
    /// Si la lista está vacía, el trigger nunca dispara (fail-safe).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnTrioOrBetter : IOnComboMatchedTrigger
    {
        [Title("Gold Amount")]
        [InfoBox("Cuánto oro se le suma al jugador cuando matchea trío o mejor.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        [Title("Trio+ Combo IDs")]
        [InfoBox("IDs de combos que cuentan como trío o mejor " +
                 "(ej. 'combo.trio', 'combo.poker', 'combo.generala'). " +
                 "Si está vacía, el trigger nunca dispara.")]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        public List<string> TrioComboIds = new List<string>();

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (TrioComboIds == null || TrioComboIds.Count == 0) return;
            if (string.IsNullOrEmpty(ctx.ComboId)) return;
            if (!TrioComboIds.Contains(ctx.ComboId)) return;

            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}