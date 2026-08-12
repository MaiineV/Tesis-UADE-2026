using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Fuerza Bruta — matchea únicamente cuando <b>los 5 dados</b> de la tirada caen en la
    /// mitad superior de su propio rango (misma regla que <c>RelativeHalfFilter</c>:
    /// <c>valor &gt; MaxFace/2</c>; d6:{4,5,6}, d8:{5..8}, d12:{7..12}). No depende de
    /// coincidencia (Par/Trío/Póker) ni de orden (Escalera) — depende pura y exclusivamente
    /// de magnitud, spec de Santi (2026-07-13). <c>BaseDamage</c> del resultado = piso plano;
    /// los 5 valores entran al daño UNA sola vez vía Σcaras de la fórmula v3 (Fix#0047 — antes
    /// iban también dentro del base y se contaban doble). <c>CountUsed = 5</c> cuando matchea.
    /// <para>
    /// Es el único combo cuya regla depende del <see cref="DiceType"/> de cada dado, no solo
    /// del valor — usa los overloads tipados de <see cref="BaseComboSO"/>. <b>Fallback</b>:
    /// si el call site no provee tipos (paths legacy, tests), asume d6 (mitad superior = 4+),
    /// el dado baseline del juego.
    /// </para>
    /// <para>
    /// Rol de balance: combo "consuelo" — base plana baja (GD: 5) lo deja debajo de Par en
    /// prioridad, así solo gana cuando ningún combo de grupo matchea. El asset puede clonarse
    /// con otra base para clases que lo quieran más arriba en la jerarquía.
    /// </para>
    /// <para>
    /// <b>Requiere los 5 dados de la bolsa, no un subset "kept".</b> A diferencia de Par/Trío/
    /// Póker (que evalúan sobre <c>keptDice</c>, el subset que el jugador elige usar), Fuerza
    /// Bruta exige el largo completo (<see cref="DiceBagSO.RequiredSize"/>) además de que todos
    /// estén en mitad superior — si no, matcheaba con solo 3 dados "kept" en mitad alta (bug
    /// reportado por Bocco, 2026-07-14).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Fuerza Bruta", fileName = "Combo_FuerzaBruta")]
    public class Combo_FuerzaBruta : BaseComboSO
    {
        [Title("Fuerza Bruta — base configurable")]
        [SerializeField, Range(0, 500)]
        [Tooltip("Piso plano del combo (término comboBase de la fórmula v3). GD default: 5. " +
                 "Los valores de los dados NO van acá: la fórmula ya los suma una vez vía " +
                 "Σcaras (los 5 dados son contribuyentes).")]
        protected int _baseDamageConfigurable = 5;

        /// <summary>Piso plano configurable (término comboBase de la fórmula v3).</summary>
        public int BaseDamageConfigurable => _baseDamageConfigurable;

        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
            => Matches(finalDice, null);

        /// <inheritdoc />
        public override bool Matches(int[] finalDice, IReadOnlyList<DiceType> diceTypes)
        {
            if (finalDice == null || finalDice.Length != DiceBagSO.RequiredSize) return false;
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
        /// Formula del combo (Fix#0047): <c>BaseDamage = piso plano</c> (override de tabla o
        /// campo del SO), <c>CountUsed = 5</c>. Match solo si <b>todos</b> los dados estan en
        /// mitad superior — no hay subconjunto parcial. <c>ContributingIndices</c> = todos los
        /// índices: la fórmula v3 suma las 5 caras UNA vez vía Σcaras. La suma va en
        /// <c>DynamicBonus</c> solo para la formula B legacy (Force Door / Heal).
        /// </summary>
        public override ComboDetectionResult Detect(IReadOnlyList<int> diceValues,
            IReadOnlyList<DiceType> diceTypes, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count != DiceBagSO.RequiredSize) return ComboDetectionResult.NoMatch();
            var hitIndices = new List<int>();
            int sum = 0;
            for (int i = 0; i < diceValues.Count; i++)
            {
                if (!IsUpperHalf(diceValues[i], TypeAt(diceTypes, i))) return ComboDetectionResult.NoMatch();
                hitIndices.Add(i);
                sum += diceValues[i];
            }
            return ComboDetectionResult.Match(
                ComboId, flatBaseOverride ?? _baseDamageConfigurable, hitIndices.Count, hitIndices,
                dynamicBonus: sum);
        }

        /// <summary>Regla canonica de mitad superior (<c>RelativeHalfFilter</c>): valor &gt; MaxFace/2.</summary>
        private static bool IsUpperHalf(int value, DiceType type)
            => value > type.MaxFace() / 2;

        // Fallback d6 cuando el call site no provee tipos o el array viene desalineado.
        private static DiceType TypeAt(IReadOnlyList<DiceType> diceTypes, int index)
            => (diceTypes != null && index < diceTypes.Count) ? diceTypes[index] : DiceType.D6;
    }
}
