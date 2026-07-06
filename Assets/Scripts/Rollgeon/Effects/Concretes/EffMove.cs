using System;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffMove : BaseEffect, IHasTooltipInfo
    {
        public override string GetEffectName() => "Move";

        public string BuildTooltip()
        {
            var selection = GetSelection();
            if (selection == null) return "Moverse";
            if (selection.IsGlobal) return "Moverse a cualquier casilla libre de la sala";
            return $"Moverse hasta {selection.Range} casillas";
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context?.SelectionResult?.FirstSelectedCoord == null) return false;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement)) return false;

            return movement.Move(context.SourceGuid, context.SelectionResult.FirstSelectedCoord.Value);
        }
    }
}
