using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Sub-struct del <see cref="RulesetSO"/> con los parámetros del initiative
    /// speed-die (TECHNICAL.md §12.7, §14.7).
    /// </summary>
    /// <remarks>
    /// Consumido por <c>DefaultInitiativeProvider</c>. Los campos se exponen con
    /// atributos Odin listos para que la designer tool Tool#0100T los renderice
    /// directamente (plan §7). La validación <c>Min ≤ Max</c> se hace en
    /// <see cref="OnValidate"/>.
    /// </remarks>
    [Serializable]
    public struct TurnOrderConfig
    {
        [Title("Initiative")]
        [MinValue(1)]
        [PropertyRange(1, 20)]
        [Tooltip("Cara mínima del die de initiative. GDD: 1. El provider suma Speed + roll(Min..Max).")]
        public int SpeedDieMin;

        [MinValue(1)]
        [PropertyRange(1, 20)]
        [Tooltip("Cara máxima del die de initiative. Debe ser >= SpeedDieMin. GDD default: 6.")]
        public int SpeedDieMax;

        [Tooltip("Valor de initiative usado cuando una entidad participante no tiene stat Speed " +
                 "(ej: props inertes que participan como target). Permite al diseñador subir/bajar " +
                 "la prioridad de corpses/props sin tocar código.")]
        [PropertyRange(-10, 10)]
        public int FallbackInitiativeForMissingSpeed;

        [Title("Pacing")]
        [PropertyRange(0f, 3f)]
        [SuffixLabel("s", overlay: true)]
        [Tooltip("Grace period antes de CADA acción enemiga (CNF-006): le da al jugador un " +
                 "instante para leer qué enemigo va a actuar antes de que el handler dispare. " +
                 "0 = sin delay (comportamiento síncrono legacy).")]
        public float EnemyActionDelaySeconds;

        /// <summary>
        /// Hook de validación — el Odin editor lo invoca en edit time, y
        /// <see cref="RulesetSO"/> lo llama desde su propio <c>OnValidate</c>.
        /// </summary>
        public void OnValidate()
        {
            if (SpeedDieMin < 1)
            {
                SpeedDieMin = 1;
            }
            if (SpeedDieMax < SpeedDieMin)
            {
                SpeedDieMax = SpeedDieMin;
            }
            if (EnemyActionDelaySeconds < 0f)
            {
                EnemyActionDelaySeconds = 0f;
            }
        }
    }
}
