using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Fuerza Bruta — suma los valores de todos los dados cuya cara cayo en la <b>mitad
    /// superior de su propio rango</b> (misma regla que <c>RelativeHalfFilter</c>:
    /// <c>valor &gt; MaxFace/2</c>; d6:{4,5,6}, d8:{5..8}, d12:{7..12}). Dano resultante:
    /// <c>BaseDamage + Σ(valores en mitad superior)</c>. <c>CountUsed = hits</c> (variable).
    /// <para>
    /// Es el unico combo cuya regla depende del <see cref="DiceType"/> de cada dado, no solo
    /// del valor — usa los overloads tipados de <see cref="BaseComboSO"/>. <b>Fallback</b>:
    /// si el call site no provee tipos (paths legacy, tests), asume d6 (mitad superior = 4+),
    /// el dado baseline del juego.
    /// </para>
    /// <para>
    /// Rol de balance: combo "consuelo" — base plana baja (GD: 5) lo deja debajo de Par en
    /// prioridad, asi solo gana cuando ningun combo de grupo matchea. El asset puede clonarse
    /// con otra base para clases que lo quieran mas arriba en la jerarquia.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Fuerza Bruta", fileName = "Combo_FuerzaBruta")]
    public class Combo_FuerzaBruta : BaseComboSO
    {
        [Title("Fuerza Bruta — base configurable")]
        [SerializeField, Range(0, 500)]
        [Tooltip("Piso plano que se suma encima de la suma de los dados en mitad superior. " +
                 "GD default: 5 (rol consuelo, debajo de Par). El daño final es " +
                 "BaseDamageConfigurable + Σ(valores en mitad superior).")]
        protected int _baseDamageConfigurable = 5;

        /// <summary>Piso plano configurable (se suma encima de la suma dinamica).</summary>
        public int BaseDamageConfigurable => _baseDamageConfigurable;

        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
            => Matches(finalDice, null);

        /// <inheritdoc />
        public override bool Matches(int[] finalDice, IReadOnlyList<DiceType> diceTypes)
        {
            if (finalDice == null) return false;
            for (int i = 0; i < finalDice.Length; i++)
            {
                if (IsUpperHalf(finalDice[i], TypeAt(diceTypes, i))) return true;
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
                if (IsUpperHalf(finalDice[i], DiceType.D6)) hits++;
            }
            return hits;
        }

        /// <inheritdoc />
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues, int? flatBaseOverride)
            => Detect(diceValues, null, flatBaseOverride);

        /// <summary>
        /// Formula del combo: <c>BaseDamage = piso + Σ(valores en mitad superior)</c>,
        /// <c>CountUsed = hits</c>. <c>ContributingIndices</c> (Spec de Daño v2) = los índices
        /// exactos de los dados en mitad superior — son los únicos que entran a
        /// <c>multi_dmg_combo</c>. El override de la tabla por clase reemplaza solo el piso
        /// plano; la suma dinámica va encima.
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues,
            IReadOnlyList<DiceType> diceTypes, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            var hitIndices = new List<int>();
            int sum = 0;
            for (int i = 0; i < diceValues.Count; i++)
            {
                if (!IsUpperHalf(diceValues[i], TypeAt(diceTypes, i))) continue;
                hitIndices.Add(i);
                sum += diceValues[i];
            }
            if (hitIndices.Count == 0) return ComboDetectionResult.NoMatch();
            return ComboDetectionResult.Match(
                ComboId, (flatBaseOverride ?? _baseDamageConfigurable) + sum, hitIndices.Count, hitIndices);
        }

        /// <summary>Regla canonica de mitad superior (<c>RelativeHalfFilter</c>): valor &gt; MaxFace/2.</summary>
        private static bool IsUpperHalf(int value, DiceType type)
            => value > type.MaxFace() / 2;

        // Fallback d6 cuando el call site no provee tipos o el array viene desalineado.
        private static DiceType TypeAt(IReadOnlyList<DiceType> diceTypes, int index)
            => (diceTypes != null && index < diceTypes.Count) ? diceTypes[index] : DiceType.D6;
    }
}
