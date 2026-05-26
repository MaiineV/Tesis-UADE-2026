using System;
using System.Collections.Generic;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Suma bonus plano al daño del combo. Hook: <c>OnComboMatched</c>.
    /// Cubre el "Multiplicador de puntaje" del GDD ("Si dicho dado es parte de un
    /// combo suma X valor al multiplicador del combo") — semántica MVP: suma plana
    /// al combo, no per-die.
    /// </summary>
    /// <remarks>
    /// El amount se lee via <see cref="EffectIntReader"/> — designer puede usar
    /// <c>ReadConstantInt</c>, <c>ReadComboCounter</c> (escalar con run), etc.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddComboDamage : IOnComboMatchedTrigger
    {
        [Title("Bonus")]
        [InfoBox("Cuánto suma al daño del combo. Usá ReadConstantInt para fijo, " +
                 "ReadComboCounter('combo.par') para escalar con la run, etc.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        [Title("Filter (optional)")]
        [InfoBox("Si se llena, el bonus solo se aplica cuando matchea uno de estos combos. " +
                 "Empty = aplica a cualquier combo.")]
        public List<string> RestrictToComboIds = new List<string>();

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (!PassesFilter(ctx)) return;
            int amount = Bonus != null ? Bonus.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusComboDamage += amount;
        }

        private bool PassesFilter(EnchantmentTriggerContext ctx)
        {
            if (RestrictToComboIds == null || RestrictToComboIds.Count == 0) return true;
            if (string.IsNullOrEmpty(ctx.ComboId)) return false;
            return RestrictToComboIds.Contains(ctx.ComboId);
        }
    }
}
