using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Par — detecta al menos un grupo de dos dados iguales. <c>CountUsed = 2</c>.
    /// Base del GD: 10 (editable en inspector).
    /// <para>
    /// Nota (plan §10.5): un Trio / Poker / Generala tambien matchea como Par (<c>count ≥ 2</c>).
    /// La resolucion de "combo mas alto" la hace downstream via <see cref="BaseComboSO.Priority"/>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Par", fileName = "Combo_Par")]
    public class Combo_Par : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 2) return false;
            return finalDice.GroupBy(d => d).Any(g => g.Count() >= 2);
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 2;
    }
}
