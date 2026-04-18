using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combos.Counters
{
    /// <summary>
    /// Sub-sección de <see cref="Rollgeon.Balance.RulesetSO"/> con las perillas del
    /// sistema de <b>Combo Counters</b> (TECHNICAL.md §5.5).
    /// <para>
    /// Balatro-style: cada match acumula <c>PerUseBonus</c> al multiplicador, capado por
    /// <c>MaxBonus</c>. Defaults: <c>+2% por uso</c>, <c>+20% techo</c> (10 usos para llegar
    /// al tope). El designer lo tuneará en el inspector del <c>Ruleset.asset</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fórmula.</b> <c>multiplier = 1 + min(MaxBonus, Count * PerUseBonus)</c>. Count = 0
    /// devuelve <c>1.0f</c> (no-op). El cap se aplica antes del <c>+1</c>, por lo que el
    /// multiplicador efectivo queda en <c>[1, 1 + MaxBonus]</c>.
    /// </para>
    /// <para>
    /// Vive físicamente en <c>Combos/Counters/</c> para mantener el código del sistema junto,
    /// pero se expone al Inspector via <see cref="Rollgeon.Balance.RulesetSO"/> (merge-hook).
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ComboCountersConfig
    {
        [Tooltip("Bonus añadido al multiplicador por cada match exitoso del combo. " +
                 "Ej. 0.02 = +2% por uso. La fórmula es: mult = 1 + min(MaxBonus, Count * PerUseBonus).")]
        [Range(0f, 1f)]
        [MinValue(0f)]
        public float PerUseBonus = 0.02f;

        [Tooltip("Techo del bonus acumulado (independiente del count). Ej. 0.20 = +20% máximo. " +
                 "Un combo con cap=0.20 y PerUseBonus=0.02 llega al techo tras 10 usos.")]
        [Range(0f, 10f)]
        [MinValue(0f)]
        public float MaxBonus = 0.20f;

        /// <summary>
        /// Computa el multiplicador final para un count dado. <c>count &lt;= 0</c> ⇒ <c>1.0f</c>.
        /// </summary>
        public float ComputeMultiplier(int count)
        {
            if (count <= 0) return 1f;
            if (PerUseBonus <= 0f) return 1f;

            float raw = count * PerUseBonus;
            float capped = raw > MaxBonus ? MaxBonus : raw;
            return 1f + capped;
        }

        /// <summary>Clamp defensivo si el SO quedó con configuración inválida.</summary>
        public void Validate()
        {
            if (PerUseBonus < 0f) PerUseBonus = 0f;
            if (MaxBonus < 0f) MaxBonus = 0f;
        }
    }
}
