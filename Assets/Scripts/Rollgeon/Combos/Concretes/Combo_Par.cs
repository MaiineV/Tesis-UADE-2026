using System;
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

        // Spec de Daño v2: multi_dmg_combo necesita los dados EXACTOS del par, no toda la
        // tirada. Si hay un Trio/Poker/Generala (que también matchean acá), nos quedamos con
        // el grupo de mayor valor entre los que califican como par — determinístico.
        /// <inheritdoc />
        protected override int[] GetContributingIndices(int[] finalDice)
        {
            if (finalDice == null) return Array.Empty<int>();
            var group = finalDice
                .Select((value, index) => (value, index))
                .GroupBy(t => t.value)
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Key)
                .FirstOrDefault();
            return group == null ? Array.Empty<int>() : group.Take(2).Select(t => t.index).ToArray();
        }
    }
}
