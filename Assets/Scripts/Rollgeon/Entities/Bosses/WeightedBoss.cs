using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    /// <summary>
    /// Entry de un <see cref="BossPoolSO"/>: un boss + su peso en el roll del piso.
    /// Mismo patrón que <c>WeightedEnchantment</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El peso vive acá (no en el <see cref="EnemyDataSO"/>) porque un mismo boss puede
    /// entrar a pools de pisos distintos con pesos distintos.
    /// </para>
    /// <para>
    /// <b>Dos palancas para desactivar.</b> El diseño pide poder bajar el peso a 0 y
    /// también apagar la entry con un toggle explícito: <see cref="Weight"/> es la palanca
    /// de tuning (rolear menos/nada) y <see cref="Enabled"/> la de contenido (el boss no
    /// está listo / está fuera del build). Cualquiera de las dos lo saca del roll.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class WeightedBoss
    {
        [Required]
        [Tooltip("El boss elegible. Required.")]
        public EnemyDataSO Boss;

        [MinValue(0f)]
        [Tooltip("Peso relativo dentro del pool del piso. 0 = nunca se rolea " +
                 "(deshabilitar por tuning sin borrar la entry).")]
        public float Weight = 1f;

        [Tooltip("Off = la entry queda fuera del roll sin tocar su peso " +
                 "(deshabilitar por contenido: boss no listo / fuera del build).")]
        public bool Enabled = true;
    }
}
