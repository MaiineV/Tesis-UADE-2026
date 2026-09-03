using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    public enum DiceParity
    {
        Odd,
        Even,
    }

    /// <summary>
    /// Cantidad de dados de la TIRADA (<c>DiceResult</c>, no solo los holdeados) con la
    /// paridad pedida × <see cref="PerDieAmount"/>. Para "Bolsa del Impar" (GDD: +3 de oro
    /// por cada dado impar, con o sin combo). Lleva su propio factor porque
    /// <c>EffModifyGold</c> no escala readers.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadDiceCountByParity : EffectIntReader
    {
        [Tooltip("Qué dados cuentan. Bolsa del Impar: Odd.")]
        public DiceParity Parity = DiceParity.Odd;

        [Tooltip("Cuánto vale cada dado que cumple. Bolsa del Impar: 3 (oro).")]
        public int PerDieAmount = 3;

        public override int Read(EffectContext context)
        {
            var dice = context?.DiceResult;
            if (dice == null) return 0;
            int wanted = Parity == DiceParity.Odd ? 1 : 0;
            int count = 0;
            for (int i = 0; i < dice.Count; i++)
                if (Math.Abs(dice[i]) % 2 == wanted) count++;
            return count * PerDieAmount;
        }
    }
}
