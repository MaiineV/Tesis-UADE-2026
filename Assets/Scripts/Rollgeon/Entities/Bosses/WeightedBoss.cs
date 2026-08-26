using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    /// <summary>
    /// El peso vive acá (no en el <see cref="EnemyDataSO"/>) porque un mismo boss puede entrar a
    /// pools de pisos distintos con pesos distintos. <see cref="Weight"/> es la palanca de tuning y
    /// <see cref="Enabled"/> la de contenido; cualquiera de las dos lo saca del roll. El roll del
    /// boss manda y <see cref="Room"/> viene con él: dos entries pueden compartir sala sin duplicar
    /// el asset.
    /// </summary>
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

        [Tooltip("Sala donde se pelea este boss. Vacío = la sala se sortea del pool de salas " +
                 "del piso, como antes.")]
        public Dungeon.RoomSO Room;
    }
}
