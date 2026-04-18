using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// [STUB] — T99 will extend with stats / behaviors / loot / portrait / etc.
    /// <para>
    /// Este worktree (Content#0097b) define solo los 4 campos minimos necesarios para
    /// cablear el sistema de weakness-hit (TECHNICAL.md §5). T99 va a <b>agregar</b>
    /// campos; no va a renombrar ni quitar los existentes.
    /// </para>
    /// <para>
    /// <b>Hereda <see cref="SerializedScriptableObject"/></b> para que T99 pueda agregar
    /// campos polimorficos (behaviors, loot tables, etc.) sin re-trabajar la serializacion.
    /// </para>
    /// </summary>
    // [STUB] — T99 will extend with stats/behaviors. No renombrar los 4 campos actuales.
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Enemy Data (STUB)", fileName = "EnemyData")]
    public class EnemyDataSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Delayed]
        [Tooltip("Id canonico del enemigo (ej. 'enemy.support.auditor').")]
        public string EntityId;

        [Delayed]
        [Tooltip("Nombre legible para UI.")]
        public string DisplayName;

        [Title("Weakness (§5 — T97b)")]
        [ValueDropdown(nameof(GetComboIds))]
        [Tooltip("ComboId al que este enemigo es debil. Vacio = sin debilidad. " +
                 "Se alimenta del ComboCatalogSO registrado en ServiceLocator.")]
        public string WeaknessComboId;

        [MinValue(0f)]
        [Range(0f, 5f)]
        [Tooltip("Override del multiplier global de weakness. 0 = usar RulesetSO.Weakness.DefaultMultiplier. " +
                 ">0 pisa el default global solo para este enemigo.")]
        public float WeaknessMultiplierOverride = 0f;

        // ---- Odin dropdown source (same pattern as BaseComboSO) ---------

        private static IEnumerable<string> GetComboIds()
        {
            if (ServiceLocator.TryGetService<ComboCatalogSO>(out var cat) && cat != null)
            {
                return cat.AllIds;
            }
            return Array.Empty<string>();
        }
    }
}
