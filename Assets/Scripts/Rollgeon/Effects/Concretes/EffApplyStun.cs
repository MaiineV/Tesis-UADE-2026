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
    /// Aturde a los targets resueltos por <c>Turns</c> turnos vía <see cref="IStunService"/>
    /// (Feature#0085: choques de Justa de Justicia / cadenas de Bottle'o Thunder).
    /// </summary>
    /// <remarks>
    /// Resolución de target = mismo criterio que <see cref="EffGridPush"/>: celdas
    /// seleccionadas → ocupantes dedup; sin selección, <c>context.TargetGuid</c>. Sin
    /// <see cref="IStunService"/> registrado: warning + <c>true</c> — el roll ya se pagó,
    /// este efecto nunca corta la cadena.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffApplyStun : BaseEffect
    {
        [Title("Aturdir")]
        [SerializeField, MinValue(1)]
        [Tooltip("Turnos de Aturdido que aplica a cada target resuelto.")]
        private int _turns = 1;

        public override string GetEffectName() => "Apply Stun";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var targets = ResolveTargetGuids(context);
            if (targets.Count == 0) return true;

            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null)
            {
                Debug.LogWarning("[EffApplyStun] IStunService no registrado — nadie pierde el turno.");
                return true;
            }

            foreach (var target in targets)
            {
                stun.ApplyStun(target, _turns);
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
