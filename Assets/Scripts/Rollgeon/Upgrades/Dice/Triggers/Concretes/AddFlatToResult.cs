using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Pesado": suma +N plano al resultado final del dado. Hook: <c>OnRollResolved</c>.
    /// El amount se lee via <see cref="EffectIntReader"/> — designer configura
    /// <c>ReadConstantInt(2)</c> para el caso base del GDD (+2).
    /// </summary>
    /// <remarks>
    /// <b>Aproximacion MVP.</b> Se aplica como bonus al daño del combo via scratch.
    /// Para modificacion per-die del resultado, ver Phase 4.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddFlatToResult : IOnRollResolvedTrigger
    {
        [Title("Bonus")]
        [InfoBox("Cuanto se suma al resultado del dado. ReadConstantInt(2) para el " +
                 "caso base del GDD 'Pesado (+2)'.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            // Suma plana via scratch; tratada como bonus de combo damage.
            // Para modificacion per-die del resultado, ver Phase 4.
            int amount = Bonus != null ? Bonus.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusComboDamage += amount;
        }
    }
}