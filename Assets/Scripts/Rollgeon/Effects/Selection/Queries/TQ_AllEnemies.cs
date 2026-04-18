using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Stubs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Selection.Queries
{
    /// <summary>
    /// Query AoE — todos los enemigos del owner. TECHNICAL.md §11.2b.
    /// Consulta <see cref="IEntityQueryService"/> via <see cref="ServiceLocator"/>.
    /// <para>
    /// Fallback defensivo (plan §3.2): si el service no está registrado (ej. en tests
    /// unitarios sin bootstrap), devuelve lista vacía + <see cref="Debug.LogWarning"/>.
    /// Esto evita <c>KeyNotFoundException</c> en código que crea queries fuera de runtime.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class TQ_AllEnemies : BaseTargetQuery
    {
        public override string QueryName => "All Enemies";

        public override List<TargetRef> Evaluate(TargetQueryContext context)
        {
            var result = new List<TargetRef>();
            if (context == null) return result;

            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var service))
            {
                Debug.LogWarning(
                    "[TQ_AllEnemies] IEntityQueryService not registered — returning empty target list. " +
                    "Register the real service in bootstrap (TECHNICAL.md §1.1) to resolve enemies at runtime.");
                return result;
            }

            var enemies = service.GetAllEnemiesOf(context.OwnerGuid);
            if (enemies == null) return result;

            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                result.Add(TargetRef.Entity(enemy.Guid));
            }
            return result;
        }
    }
}
