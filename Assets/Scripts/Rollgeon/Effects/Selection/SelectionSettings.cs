using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Struct de configuración de selección embebido dentro de cada <see cref="IEffect"/>
    /// que implemente <see cref="IUsesSelection"/>. TECHNICAL.md §11.2.
    /// <para>
    /// <see cref="TargetQuery"/> es un campo polimórfico — lleva <c>[OdinSerialize]</c> +
    /// <c>[SerializeReference]</c> según §13.6.1 para que el subtipo concreto sobreviva al
    /// round-trip de save/load. <c>[HideReferenceObjectPicker]</c> aparece en la clase base
    /// <see cref="BaseTargetQuery"/>, no acá.
    /// </para>
    /// </summary>
    [Serializable]
    public class SelectionSettings
    {
        [Tooltip("El efecto requiere un target seleccionado antes de aplicarse.")]
        public bool RequiresSelection;

        [Tooltip("Cuándo se resuelve la selección relativa al TryExecute.")]
        public SelectionTiming Timing = SelectionTiming.BeforeResolve;

        [Tooltip("true → la cantidad de targets es literalmente SelectionCount. " +
                 "false → se resuelve dinámicamente via reader (TODO downstream).")]
        public bool IsConstantSelectionCount = true;

        [MinValue(1), MaxValue(16)]
        [Tooltip("Cantidad de targets requeridos cuando IsConstantSelectionCount == true.")]
        public int SelectionCount = 1;

        [Tooltip("Si la selección es skippeada o cancelada, el efecto no falla — se trata como no-op.")]
        public bool IsSkippable;

        [Tooltip("Sólo son válidos los slots vacíos (movimiento, teleport).")]
        public bool RequireEmptySlot;

        [Tooltip("Sólo son válidos los slots ocupados (ataques, interacciones).")]
        public bool RequireOccupiedSlot;

        /// <summary>
        /// Query inline polimórfica. El doble atributo <c>[OdinSerialize, SerializeReference]</c>
        /// cumple §13.6.1 — Odin serializa el subtipo en editor/save polimórficamente y
        /// <c>[SerializeReference]</c> activa la ruta nativa de Unity para prefab overrides.
        /// </summary>
        [OdinSerialize, SerializeReference]
        public BaseTargetQuery TargetQuery;

        /// <summary>
        /// Resuelve la cantidad efectiva de targets. En esta foundation devuelve
        /// <see cref="SelectionCount"/> — si <see cref="IsConstantSelectionCount"/> es false,
        /// downstream readers overridean este método (la API queda congelada).
        /// </summary>
        public int GetSelectionCount(ReadInfo info)
        {
            // [TODO downstream] — si IsConstantSelectionCount == false, leer del reader asociado.
            return SelectionCount;
        }

        /// <summary>
        /// True si la selección debe resolverse al timing dado. Consumido por el dispatcher
        /// del behavior para saber si pre-resolver (BeforeResolve) o delegar al ApplyEffect
        /// (DuringResolve).
        /// </summary>
        public bool NeedsSelectionAt(SelectionTiming t)
        {
            return RequiresSelection && Timing == t;
        }
    }
}
