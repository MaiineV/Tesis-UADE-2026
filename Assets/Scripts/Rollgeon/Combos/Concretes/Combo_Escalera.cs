using System.Linq;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Escalera — cinco valores consecutivos (orden-agnostico). <c>CountUsed = 5</c>. Base del GD: 35.
    /// <para>
    /// Acepta <c>[1,2,3,4,5]</c>, <c>[2,3,4,5,6]</c> y para d8+ futuro <c>[3,4,5,6,7]</c>, etc.
    /// Normalizacion interna (plan §5.4): <c>Distinct().OrderBy()</c>. Test §9.2 cubre orden mezclado.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Escalera", fileName = "Combo_Escalera")]
    public class Combo_Escalera : BaseComboSO
    {
        private const int StraightLength = 5;

        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < StraightLength) return false;
            var distinct = finalDice.Distinct().OrderBy(d => d).ToArray();
            if (distinct.Length != StraightLength) return false;
            return (distinct[StraightLength - 1] - distinct[0]) == (StraightLength - 1);
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => StraightLength;
    }
}
