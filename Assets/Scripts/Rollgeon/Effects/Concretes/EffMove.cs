using System;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Localization;
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
            if (selection == null)
                return LocalizedContent.Ui("tooltip.effect.move.default", "Moverse");
            if (selection.IsGlobal)
                return LocalizedContent.Ui("tooltip.effect.move.global",
                    "Moverse a cualquier casilla libre de la sala");
            return string.Format(
                LocalizedContent.Ui("tooltip.effect.move.range", "Moverse hasta {0} casillas"),
                selection.Range);
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context?.SelectionResult?.FirstSelectedCoord == null) return false;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement)) return false;

            return movement.Move(context.SourceGuid, context.SelectionResult.FirstSelectedCoord.Value);
        }
    }
}
