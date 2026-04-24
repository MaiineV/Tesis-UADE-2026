using System;
using Patterns;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Aplica un impulso (knockback) al target escribiéndolo en el bag del behavior bajo
    /// <see cref="BehaviorValueKey.HitImpulse"/>. La capa que físicamente mueve al pawn
    /// (feedback / physics / animator) consume el valor cuando llegue §10.
    /// TECHNICAL.md §9.2, §9.5.
    /// </summary>
    /// <remarks>
    /// Atómico: dirección + magnitud authored como <see cref="Vector3"/>; no intenta
    /// derivar la dirección de source→target para mantenerse independiente del spacing
    /// runtime. Un effect más alto nivel podría composerse leyendo posiciones del
    /// <c>IGridManager</c> y parseando hacia este effect, pero no es responsabilidad de
    /// este concreto.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffApplyImpulse : BaseEffect,
        IUsesSelection, IShouldStoreValuesOnBehavior
    {
        [Title("Impulse")]
        [SerializeField]
        [Tooltip("Vector de impulso en world space (magnitud + dirección).")]
        private Vector3 _impulse = new Vector3(0f, 0f, 1f);

        public override string GetEffectName() => "Apply Impulse";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (context.SourceBehavior == null)
            {
                Debug.LogWarning("[EffApplyImpulse] SourceBehavior null — no hay bag donde escribir.");
                return false;
            }

            var targetGuid = ResolveTargetGuid(context);

            context.SourceBehavior.SetBehaviorValue(
                BehaviorValueKey.HitImpulse,
                new ImpulseBehaviorValue
                {
                    Impulse = _impulse,
                    TargetEntityGuid = targetGuid,
                });

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult?.FirstSelectedCoord is Grid.GridCoord coord
                && ServiceLocator.TryGetService<Grid.IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                return occupant;
            return context.TargetGuid;
        }
    }
}
