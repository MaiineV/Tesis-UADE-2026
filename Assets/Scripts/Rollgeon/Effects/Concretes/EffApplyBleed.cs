using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Agrega stacks de Sangrado a los targets resueltos vía <see cref="IBleedService"/>
    /// (Feature#0084: Blood Transfusion cuando el pool de sangrado tiene un único elegible).
    /// </summary>
    /// <remarks>
    /// Resolución de target = mismo criterio que <see cref="EffGridPush"/>: celdas
    /// seleccionadas → ocupantes dedup; sin selección, <c>context.TargetGuid</c>. La fuente
    /// del stack es <c>context.SourceGuid</c> (el crédito de kill del tick). Sin
    /// <see cref="IBleedService"/> registrado: warning + <c>true</c> — el roll ya se pagó,
    /// este efecto nunca corta la cadena.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffApplyBleed : BaseEffect
    {
        [Title("Sangrado")]
        [SerializeField, MinValue(1)]
        [Tooltip("Stacks de Sangrado que agrega a cada target resuelto (se SUMAN a los que ya tenga).")]
        private int _stacks = 1;

        public override string GetEffectName() => "Apply Bleed";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var targets = ResolveTargetGuids(context);
            if (targets.Count == 0) return true;

            if (!ServiceLocator.TryGetService<IBleedService>(out var bleed) || bleed == null)
            {
                Debug.LogWarning("[EffApplyBleed] IBleedService no registrado — no sangra nadie. " +
                                 "Agregá BleedServiceBootstrap a ExtraServices.");
                return true;
            }

            foreach (var target in targets)
            {
                bleed.AddStack(target, context.SourceGuid, _stacks);
            }

            return true;
        }

        // Mismo criterio que EffGridPush/EffDealDamage: celdas seleccionadas → ocupantes
        // dedup; sin selección, TargetGuid.
        private static List<Guid> ResolveTargetGuids(EffectContext context)
        {
            var result = new List<Guid>();

            if (context.SelectionResult?.SelectedTargets != null
                && ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                result = grid.DistinctOccupants(context.SelectionResult.SelectedTargets.Select(t => t.Coord));
            }

            if (result.Count == 0 && context.TargetGuid != Guid.Empty)
                result.Add(context.TargetGuid);

            return result;
        }
    }
}
