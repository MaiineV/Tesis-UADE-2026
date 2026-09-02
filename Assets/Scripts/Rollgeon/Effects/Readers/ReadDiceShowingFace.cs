using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Cantidad de dados de la jugada que muestran exactamente <see cref="Face"/> ×
    /// <see cref="PerDieAmount"/>. Para "Jackpot" (GDD: +5 de daño por cada dado con 7).
    /// Cuenta sobre los dados holdeados (<c>EffectContext.KeptDice</c>, los que participan
    /// del ataque) y cae a la tirada completa (<c>DiceResult</c>) si no hubo keep explícito.
    /// </summary>
    /// <remarks>
    /// Solo tiene sentido colgado de un hook ComboPlayed: es el único dispatch que copia
    /// los dados de la jugada al <c>EffectContext</c> (ver <c>InventoryService.BindComboPlayedHook</c>).
    /// Sin dados en el contexto → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadDiceShowingFace : EffectIntReader
    {
        [MinValue(1)]
        [Tooltip("Cara que paga. Jackpot: 7 (decisión GD 2026-09-02: literal, no el máximo del dado).")]
        public int Face = 7;

        [Tooltip("Cuánto vale cada dado que muestre la cara. Jackpot: 5.")]
        public int PerDieAmount = 5;

        public override int Read(EffectContext context)
        {
            var dice = Pick(context);
            if (dice == null) return 0;
            int count = 0;
            for (int i = 0; i < dice.Count; i++)
                if (dice[i] == Face) count++;
            return count * PerDieAmount;
        }

        private static IReadOnlyList<int> Pick(EffectContext context)
        {
            if (context == null) return null;
            if (context.KeptDice != null && context.KeptDice.Count > 0) return context.KeptDice;
            return context.DiceResult;
        }
    }
}
