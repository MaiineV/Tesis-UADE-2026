using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Selection.Queries
{
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
                    "[TQ_AllEnemies] IEntityQueryService not registered — returning empty target list.");
                return result;
            }

            var enemies = service.GetAllEnemiesOf(context.OwnerGuid);
            if (enemies == null) return result;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogWarning(
                    "[TQ_AllEnemies] IGridManager not registered — cannot resolve enemy positions.");
                return result;
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                if (grid.TryGetPosition(enemy.Guid, out var pos))
                    result.Add(TargetRef.At(pos));
            }
            return result;
        }
    }
}
