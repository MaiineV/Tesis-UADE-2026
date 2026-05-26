using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Dados de suerte" del GDD — al matchear un combo, con probabilidad
    /// <c>1/OneInChance</c>, suma <see cref="Bonus"/> daño extra al combo.
    /// Hook: <c>OnComboMatched</c>.
    /// </summary>
    /// <remarks>
    /// Usa <see cref="UnityEngine.Random"/>. Para tests determinísticos, Phase 4
    /// puede inyectar un seed al <c>DiceEnchantmentService</c>; este trigger
    /// resolverá su check contra esa fuente cuando esté wireado (TODO Phase 4).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class LuckyChanceComboBonus : IOnComboMatchedTrigger
    {
        [Title("Chance")]
        [Tooltip("Denominador de la probabilidad. 5 = 1-en-5 (20%). 10 = 1-en-10 (10%). " +
                 "Default 5 sigue el GDD ('1 en 5').")]
        [MinValue(1)]
        public int OneInChance = 5;

        [Title("Bonus on Lucky Hit")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            // UnityEngine.Random.Range(0, N) → [0, N-1]. Hit cuando == 0 → prob 1/N.
            if (Random.Range(0, Mathf.Max(1, OneInChance)) != 0) return;
            int amount = Bonus != null ? Bonus.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusComboDamage += amount;
        }
    }
}
