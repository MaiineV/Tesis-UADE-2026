using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Selection.Readers
{
    /// <summary>
    /// Cantidad de targets = enemigos vivos del owner (con tope). Para efectos tipo
    /// "golpeá a todos los enemigos, hasta N".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AliveEnemiesCountReader : ISelectionCountReader
    {
        [MinValue(1)]
        [Tooltip("Tope del count aunque haya más enemigos vivos.")]
        public int MaxCount = 16;

        public int Read(ReadInfo info)
        {
            if (info.ownerGuid == Guid.Empty) return 1;

            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null)
                return 1;

            // Sin AttributesManager (tests/bootstrap temprano) se cuenta todo enemigo —
            // fallback permisivo, mismo criterio que SelectionSettings.PassesEntityFilter.
            ServiceLocator.TryGetService<AttributesManager>(out AttributesManager attrs);

            int count = 0;
            foreach (var enemy in query.GetAllEnemiesOf(info.ownerGuid))
            {
                if (enemy == null) continue;
                if (attrs != null && IsDead(attrs, enemy.Guid)) continue;
                count++;
            }

            return Mathf.Clamp(count, 1, MaxCount);
        }

        private static bool IsDead(AttributesManager attrs, Guid guid)
        {
            var health = attrs.GetAttribute<Health>(guid);
            return health != null && health.Value <= 0;
        }
    }
}
