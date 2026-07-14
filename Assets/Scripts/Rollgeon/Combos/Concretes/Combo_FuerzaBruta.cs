using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Fuerza Bruta — matchea unicamente cuando <b>los 5 dados</b> de la tirada caen en la
    /// mitad superior de su propio rango (misma regla que <c>RelativeHalfFilter</c>:
    /// <c>valor &gt; MaxFace/2</c>; d6:{4,5,6}, d8:{5..8}, d12:{7..12}). No depende de
    /// coincidencia (Par/Trio/Poker) ni de orden (Escalera) — depende pura y exclusivamente
    /// de magnitud, spec de Santi (2026-07-13). Dano resultante:
    /// <c>BaseDamage + Σ(los 5 valores)</c>. <c>CountUsed = 5</c> cuando matchea.
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
            if (finalDice == null || finalDice.Length == 0) return false;
            for (int i = 0; i < finalDice.Length; i++)
            {
                if (!IsUpperHalf(finalDice[i], TypeAt(diceTypes, i))) return false;
            }
            return true;
        }

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice)
            => finalDice?.Length ?? 0;

        /// <inheritdoc />
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues, int? flatBaseOverride)
            => Detect(diceValues, null, flatBaseOverride);

        /// <summary>
        /// Formula del combo: <c>BaseDamage = piso + Σ(los 5 valores)</c>, <c>CountUsed = 5</c>.
        /// Match solo si <b>todos</b> los dados estan en mitad superior — no hay subconjunto
        /// parcial. <c>ContributingIndices</c> (Spec de Daño v2) = todos los índices. El
        /// override de la tabla por clase reemplaza solo el piso plano; la suma dinámica va
        /// encima.
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues,
            IReadOnlyList<DiceType> diceTypes, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            var hitIndices = new List<int>();
            int sum = 0;
            for (int i = 0; i < diceValues.Count; i++)
            {
                if (!IsUpperHalf(diceValues[i], TypeAt(diceTypes, i))) return ComboDetectionResult.NoMatch();
                hitIndices.Add(i);
                sum += diceValues[i];
            }
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
