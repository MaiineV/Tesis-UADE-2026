using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// N% de chance de que el dado no cuente para el combo. Hook:
    /// <c>OnComboMatched</c>. Encantamiento "Fragil" (negativo).
    /// </summary>
    /// <remarks>
    /// Usa <see cref="UnityEngine.Random"/>; seed determinístico pendiente Phase 4.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ChanceToNotCount : IOnComboMatchedTrigger
    {
        [Title("Fail Chance")]
        [InfoBox("Probabilidad (0-1) de que el dado no cuente para el combo. " +
                 "0.5 = 50% de chance de fallo.")]
        [Range(0f, 1f)]
        public float FailChance = 0.5f;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            // Usa UnityEngine.Random; seed determinístico pendiente Phase 4.
            if (UnityEngine.Random.value < FailChance)
            {
                ctx.Scratch.BlockComboDamage = true;
            }
        }
    }
}