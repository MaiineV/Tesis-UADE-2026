using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Entry del <see cref="ComboPassivePoolSO"/>. El peso vive acá para permitir
    /// distintas frecuencias por pool (común en floor 1, raro en floor 3).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class WeightedComboPassive
    {
        [Required]
        public ComboPassiveSO Passive;

        [MinValue(0f)]
        [Tooltip("Peso relativo. 0 = nunca se rolea (útil para deshabilitar sin borrar).")]
        public float Weight = 1f;

        [MinValue(0)]
        [Tooltip("Floor mínimo en el que la entry es elegible.")]
        public int MinFloorDepth = 0;
    }
}
