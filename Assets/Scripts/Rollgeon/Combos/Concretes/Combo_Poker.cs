using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Poker — al menos un grupo de cuatro dados iguales. <c>CountUsed = 4</c>. Base del GD: 60.
    /// <para>
    /// Nota (plan §10.5): Generala tambien matchea como Poker (<c>count ≥ 4</c>). La resolucion
    /// de combo mas alto la hace downstream via <see cref="BaseComboSO.Priority"/> — y
    /// <see cref="Combo_Generala"/> tiene <c>Priority = int.MaxValue</c> para garantizarlo.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Poker", fileName = "Combo_Poker")]
    public class Combo_Poker : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 4) return false;
            return finalDice.GroupBy(d => d).Any(g => g.Count() >= 4);
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 4;
    }
}
