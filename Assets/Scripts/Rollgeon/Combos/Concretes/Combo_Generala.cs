using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Generala — todos los dados iguales. <c>CountUsed = 5</c>. Base del GD: 100.
    /// <para>
    /// <b>Priority override</b> (hard rule #8, plan §4/§10.7): <c>Priority => int.MaxValue</c>.
    /// Esto garantiza que si Generala matchea, siempre gana incluso si un designer sube
    /// por error el <c>BaseDamage</c> de Poker a un valor mayor que 100.
    /// </para>
    /// <para>
    /// Detecta si <c>dice.Distinct().Count() == 1</c> y hay al menos 5 dados. Input con menos de 5
    /// devuelve NoMatch.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Generala", fileName = "Combo_Generala")]
    public class Combo_Generala : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < 5) return false;
            return finalDice.Distinct().Count() == 1;
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 5;

        /// <inheritdoc />
        public override int Priority => int.MaxValue;
    }
}
