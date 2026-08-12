using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Suma X — combo parametrizado por valor objetivo <c>X</c>. Matchea si hay al menos un dado
    /// con valor X. <c>BaseDamage</c> del resultado = piso plano; los dados que muestran X
    /// entran al daño UNA sola vez vía Σcaras de la fórmula v3 (Fix#0047 — <c>X * hits</c> iba
    /// también dentro del base y se contaba doble). <c>CountUsed = hits</c> (variable).
    /// <para>
    /// El warrior usa <c>X = 4</c> (Suma 4 del GD). El asset puede clonarse con <c>X = 5, 6</c> para
    /// otras clases que necesiten "Suma-5" / "Suma-6" sin duplicar codigo.
    /// </para>
    /// <para>
    /// <b>Contrato especial</b> (plan §4.4): <see cref="Detect"/> se overridea para poblar
    /// <c>ContributingIndices</c> con los dados que muestran X y llevar <c>X * hits</c> en
    /// <c>DynamicBonus</c> (formula B legacy). GD canonico: <c>X = 4</c>, <c>BaseDamage = 25</c>.
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
        [Tooltip("Piso plano del combo (término comboBase de la fórmula v3). GD default: 25. " +
                 "Los dados que muestran X NO van acá: la fórmula ya los suma una vez vía Σcaras.")]
        protected int _baseDamageConfigurable = 25;

        /// <summary>Valor objetivo del combo (1..6).</summary>
        public int X => _x;

        /// <summary>Piso plano configurable (término comboBase de la fórmula v3).</summary>
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
        /// Override de <see cref="BaseComboSO.Detect(IReadOnlyList{int}, int?)"/> (Fix#0047):
        /// <c>BaseDamage = piso plano</c> (override de tabla o campo del SO),
        /// <c>CountUsed = hits</c>. <c>ContributingIndices</c> = los índices exactos de los
        /// dados que muestran X — la fórmula v3 los suma UNA vez vía Σcaras. <c>X * hits</c>
        /// va en <c>DynamicBonus</c> solo para la formula B legacy (Force Door / Heal).
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            var hitIndices = new List<int>();
            for (int i = 0; i < diceValues.Count; i++)
            {
                if (diceValues[i] == _x) hitIndices.Add(i);
            }
            if (hitIndices.Count == 0) return ComboDetectionResult.NoMatch();
            int hits = hitIndices.Count;
            return ComboDetectionResult.Match(
                ComboId, flatBaseOverride ?? _baseDamageConfigurable, hits, hitIndices,
                dynamicBonus: _x * hits);
        }
    }
}
