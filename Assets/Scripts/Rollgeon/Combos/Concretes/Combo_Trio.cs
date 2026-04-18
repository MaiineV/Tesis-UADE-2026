using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Trio — al menos un grupo de tres dados iguales. <c>CountUsed = 3</c>. Base del GD: 28.
    /// <para>
    /// Nota (plan §10.5): Poker y Generala tambien matchean como Trio (<c>count ≥ 3</c>). La
    /// resolucion de combo mas alto la hace downstream via <see cref="BaseComboSO.Priority"/>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Trio", fileName = "Combo_Trio")]
    public class Combo_Trio : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 3) return false;
            return finalDice.GroupBy(d => d).Any(g => g.Count() >= 3);
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 3;
    }
}
