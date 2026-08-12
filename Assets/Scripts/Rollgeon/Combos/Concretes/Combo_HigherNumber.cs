using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Dado más alto — combo fallback: matchea con CUALQUIER selección no vacía y usa el
    /// dado de mayor <b>valor absoluto</b>. <c>BaseDamage</c> del resultado = base plano; la
    /// cara del dado ganador entra al daño UNA sola vez vía Σcaras de la fórmula v3
    /// (Fix#0047). <c>CountUsed = 1</c>, <c>ContributingIndices = [índice del dado más alto]</c>
    /// (primera ocurrencia ante empate).
    /// <para>
    /// Con <c>Priority = BaseDamage</c> bajo (default de la base), pierde contra cualquier
    /// combo real en <c>MatchBest</c> — solo etiqueta cuando no hay nada mejor.
    /// </para>
    /// </summary>
    /// <remarks>
    /// BUG-040: <c>Combo_HighNumber.asset</c> estaba autorado como <see cref="Combo_SumaX"/>
    /// (X=4) — matcheaba dados que MUESTRAN 4 (un D4 maxeado le ganaba a un D6 en 5) y no
    /// aparecía sin ningún 4 en la selección. Esta clase reemplaza esa config.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Higher Number", fileName = "Combo_HigherNumber")]
    public class Combo_HigherNumber : BaseComboSO
    {
        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
            => finalDice != null && finalDice.Length > 0;

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => 1;

        /// <summary>
        /// Override (Fix#0047): <c>BaseDamage = flatBaseOverride ?? BaseDamage</c> plano; la
        /// cara del ganador va en <c>DynamicBonus</c> (formula B legacy) y al daño v3 entra
        /// vía Σcaras. El único índice contribuyente es el del dado de mayor valor — es el que
        /// entra a <c>multi_dmg_combo</c> y el que valida RequireCarrierParticipates.
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();

            int bestIndex = 0;
            for (int i = 1; i < diceValues.Count; i++)
            {
                if (diceValues[i] > diceValues[bestIndex]) bestIndex = i;
            }

            return ComboDetectionResult.Match(
                ComboId,
                flatBaseOverride ?? BaseDamage,
                countUsed: 1,
                contributingIndices: new[] { bestIndex },
                dynamicBonus: diceValues[bestIndex]);
        }
    }
}
