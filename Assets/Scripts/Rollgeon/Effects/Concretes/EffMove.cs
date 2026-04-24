using System;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Mueve la entidad source a la celda seleccionada en la grilla vía
    /// <see cref="IMovementService.Move"/>. TECHNICAL.md §8.7, §17.§B.
    /// </summary>
    /// <remarks>
    /// Atómico: lee la celda destino del primer <see cref="Rollgeon.Effects.Selection.TargetRef"/>
    /// con <c>HasCell = true</c> en <see cref="EffectContext.SelectionResult"/>. Si no hay
    /// celda seleccionada o <see cref="IMovementService"/> no está registrado, aborta la cadena.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffMove : BaseEffect, IUsesSelection
    {
        public EffMove()
        {
            Selection = new Rollgeon.Effects.Selection.SelectionSettings
            {
                RequiresSelection = true,
                SelectionCount = 1,
                RequireEmptySlot = true,
            };
        }

        public override string GetEffectName() => "Move";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var sourceGuid = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            if (sourceGuid == Guid.Empty) return false;

            if (context.SelectionResult == null ||
                !context.SelectionResult.TryGetFirstSelectedCell(out GridCoord destination))
            {
                UnityEngine.Debug.LogWarning("[EffMove] SelectionResult sin celda — aborta cadena.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null)
            {
                UnityEngine.Debug.LogWarning("[EffMove] IMovementService no registrado — aborta cadena.");
                return false;
            }

            return movement.Move(sourceGuid, destination);
        }
    }
}
