using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Cara más alta entre los dados que FORMAN el combo
    /// (<c>ComboResult.ContributingIndices</c> sobre <c>KeptDice ?? DiceResult</c> — mismo
    /// espacio de índices que <c>ContributingDiceResolver</c>). Para "Fuente Mágica" (GDD:
    /// el dado más alto del combo suma su cara al multiplicador, NO a la suma base).
    /// </summary>
    /// <remarks>
    /// Sin combo (o sin índices) cae al máximo de los dados holdeados, y si no hay, de la
    /// tirada. Sin dados → 0. Si todos los dados valen lo mismo, esa cara es la que cuenta.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadHighestContributingDie : EffectIntReader
    {
        public override int Read(EffectContext context)
        {
            if (context == null) return 0;
            var dice = context.KeptDice != null && context.KeptDice.Count > 0
                ? context.KeptDice
                : context.DiceResult;
            if (dice == null || dice.Count == 0) return 0;

            var indices = context.ComboResult is { IsMatch: true } combo ? combo.ContributingIndices : null;
            if (indices != null && indices.Count > 0)
            {
                int best = 0;
                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    if (idx < 0 || idx >= dice.Count) continue;
                    if (dice[idx] > best) best = dice[idx];
                }
                if (best > 0) return best;
            }
            return Max(dice);
        }

        private static int Max(IReadOnlyList<int> dice)
        {
            int best = 0;
            for (int i = 0; i < dice.Count; i++)
                if (dice[i] > best) best = dice[i];
            return best;
        }
    }
}
