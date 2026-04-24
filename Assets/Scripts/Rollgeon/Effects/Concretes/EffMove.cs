using System;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffMove : BaseEffect, IUsesSelection
    {
        public override string GetEffectName() => "Move";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context?.SelectionResult?.FirstSelectedCoord == null) return false;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement)) return false;

            return movement.Move(context.SourceGuid, context.SelectionResult.FirstSelectedCoord.Value);
        }
    }
}
