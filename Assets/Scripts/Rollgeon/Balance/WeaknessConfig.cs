using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Sub-seccion de <see cref="RulesetSO"/> con las perillas del sistema de debilidad
    /// (TECHNICAL.md §5 — weakness-hit + Content#0097b).
    /// <para>
    /// Un solo knob: <see cref="DefaultMultiplier"/>. Un enemigo puede overridearlo via
    /// <c>EnemyDataSO.WeaknessMultiplierOverride</c>.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class WeaknessConfig
    {
        [Tooltip("Multiplicador global aplicado al damage cuando el combo matcheado coincide con la " +
                 "WeaknessComboId del enemigo. Editable sin recompilar.")]
        [Range(1.0f, 5.0f)]
        public float DefaultMultiplier = 1.5f;

        /// <summary>Clamp defensivo si el SO quedo con configuracion invalida.</summary>
        public void Validate()
        {
            if (DefaultMultiplier < 1.0f) DefaultMultiplier = 1.0f;
            if (DefaultMultiplier > 5.0f) DefaultMultiplier = 5.0f;
        }
    }
}
