using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Full House — un trio mas un par de distinto valor (grupos ordenados <c>[3, 2]</c>).
    /// <c>CountUsed = 5</c>. Base del GD: 40.
    /// <para>
    /// Ejemplo §5.2 TECHNICAL.md: <c>[3,3,3,5,5]</c> → grupos <c>[3,2]</c> → match.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Full House", fileName = "Combo_FullHouse")]
    public class Combo_FullHouse : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 5) return false;
            var groups = finalDice.GroupBy(d => d)
                                  .Select(g => g.Count())
                                  .OrderByDescending(c => c)
                                  .ToArray();
            return groups.Length >= 2 && groups[0] == 3 && groups[1] == 2;
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 5;
    }
}
