using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Doble Par — dos grupos distintos con exactamente pair-size (no trio, no poker).
    /// <c>CountUsed = 4</c>. Base del GD: 18.
    /// <para>
    /// <b>Disambiguator</b> (plan §10.6, hard rule #7): exige <c>groups.Max() == 2</c> para que
    /// un FullHouse (<c>[3, 2]</c>) NO matchee tambien como DoblePar. DoblePar y FullHouse son
    /// mutuamente excluyentes por implementacion.
    /// </para>
    /// <para>
    /// Ejemplos:
    /// <list type="bullet">
    /// <item><description><c>[3,3,5,5,1]</c> → grupos <c>[2,2,1]</c>, Max=2, count de ≥2 es 2 → match.</description></item>
    /// <item><description><c>[3,3,3,5,5]</c> (FullHouse) → grupos <c>[3,2]</c>, Max=3 → NO match.</description></item>
    /// <item><description><c>[4,4,4,4,1]</c> (Poker) → grupos <c>[4,1]</c>, Max=4 → NO match.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Doble Par", fileName = "Combo_DoblePar")]
    public class Combo_DoblePar : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 4) return false;
            var groups = finalDice.GroupBy(d => d).Select(g => g.Count()).ToArray();
            if (groups.Length == 0) return false;
            int pairsOrMore = groups.Count(c => c >= 2);
            int max = groups.Max();
            // Exige al menos 2 grupos >=2 y que el mas grande sea exactamente 2 (no trio+).
            return pairsOrMore >= 2 && max == 2;
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 4;
    }
}
