using System;
using System.Collections.Generic;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma oro cuando el combo es una escalera/straight.
    /// Hook: <c>OnComboMatched</c>. Encantamiento "Mercader".
    /// </summary>
    /// <remarks>
    /// El diseñador define en <see cref="StraightComboIds"/> qué combos cuentan
    /// como escalera — típicamente combo.escalera, combo.escalera_mayor.
    /// Si la lista está vacía, el trigger nunca dispara (fail-safe).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnStraight : IOnComboMatchedTrigger
    {
        [Title("Gold Amount")]
        [InfoBox("Cuánto oro se le suma al jugador cuando matchea una escalera.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        [Title("Straight Combo IDs")]
        [InfoBox("IDs de combos que cuentan como escalera " +
                 "(ej. 'combo.escalera', 'combo.escalera_mayor'). " +
                 "Si está vacía, el trigger nunca dispara.")]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        public List<string> StraightComboIds = new List<string>();

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (StraightComboIds == null || StraightComboIds.Count == 0) return;
            if (string.IsNullOrEmpty(ctx.ComboId)) return;
            if (!StraightComboIds.Contains(ctx.ComboId)) return;

            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}