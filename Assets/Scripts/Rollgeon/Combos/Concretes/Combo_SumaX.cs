using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Suma X — combo parametrizado por valor objetivo <c>X</c>. Matchea si hay al menos un dado
    /// con valor X. Dano resultante (hard rule #9): <c>BaseDamage + X * hits</c>, donde <c>hits</c>
    /// es la cantidad de dados con valor X. <c>CountUsed = hits</c> (variable).
    /// <para>
    /// El warrior usa <c>X = 4</c> (Suma 4 del GD). El asset puede clonarse con <c>X = 5, 6</c> para
    /// otras clases que necesiten "Suma-5" / "Suma-6" sin duplicar codigo.
    /// </para>
    /// <para>
    /// <b>Contrato especial</b> (plan §4.4): <see cref="Detect"/> se overridea para devolver
    /// <c>BaseDamage + sum</c> dinamico. GD canonico: <c>X = 4</c>, <c>BaseDamage = 25</c> (piso
    /// plano que se suma encima de la suma de los 4s).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Suma X", fileName = "Combo_SumaX")]
    public class Combo_SumaX : BaseComboSO
    {
        [Title("Suma X — parametro")]
        [SerializeField, Range(1, 6)]
        [Tooltip("Valor objetivo (pip del dado). Warrior usa X=4. Rango limitado a 1..6 per hard rule #9.")]
        protected int _x = 4;

        [Title("Suma X — base configurable")]
        [SerializeField, Range(0, 500)]
        [Tooltip("Piso plano que se suma encima de la suma de los dados que muestran X. " +
                 "GD default: 25 (editable en inspector). El daño final es BaseDamageConfigurable + X * hits.")]
        protected int _baseDamageConfigurable = 25;

        /// <summary>Valor objetivo del combo (1..6).</summary>
        public int X => _x;

        /// <summary>Piso plano configurable (se suma encima de la suma dinamica).</summary>
        public int BaseDamageConfigurable => _baseDamageConfigurable;

        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null) return false;
            for (int i = 0; i < finalDice.Length; i++)
            {
                if (finalDice[i] == _x) return true;
            }
            return false;
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice)
        {
            if (finalDice == null) return 0;
            int hits = 0;
            for (int i = 0; i < finalDice.Length; i++)
            {
                if (finalDice[i] == _x) hits++;
            }
            return hits;
        }

        /// <summary>
        /// Override de <see cref="BaseComboSO.Detect"/>. Formula del contrato §4.4:
        /// <c>BaseDamage = _baseDamageConfigurable + X * hits</c>, <c>CountUsed = hits</c>.
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            int hits = 0;
            for (int i = 0; i < diceValues.Count; i++)
            {
                if (diceValues[i] == _x) hits++;
            }
            if (hits == 0) return ComboDetectionResult.NoMatch();
            return ComboDetectionResult.Match(_baseDamageConfigurable + _x * hits, hits);
        }
    }
}
