using System.Collections.Generic;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;

namespace Rollgeon.UI.HUD.DragDrop
{
    /// <summary>
    /// Reglas puras (sin runtime Unity) que gobiernan el drag-and-drop de acciones.
    /// Separadas de los MonoBehaviours para poder testearlas en EditMode.
    /// </summary>
    public static class ActionDragPolicy
    {
        /// <summary>
        /// Sólo se puede iniciar un drag desde un botón <see cref="ActionButtonState.Available"/>.
        /// El resto de los estados significan que la acción no está lista o que hay otra
        /// selección/roll/ActionRoll en curso; arrancar un drag ahí re-entraría en el flujo
        /// (cancel-by-reselection, etc.). Este gate es load-bearing.
        /// </summary>
        public static bool CanBeginDrag(ActionButtonState state) => state == ActionButtonState.Available;

        /// <summary>La celda soltada es válida si pertenece al set de celdas válidas de la acción.</summary>
        public static bool IsValidDrop(GridCoord coord, ISet<GridCoord> validCoords)
            => validCoords != null && validCoords.Contains(coord);

        /// <summary>
        /// Una acción requiere soltar sobre una celda (targeting por tile) cuando su selección
        /// BeforeRoll necesita interacción del jugador, se auto-confirma y pide un único target —
        /// el caso que un solo drop puede satisfacer (ej. Movimiento). Self / AutoResolve /
        /// multi-target quedan afuera (no se resuelven con un drop).
        /// </summary>
        public static bool RequiresTileDrop(SelectionSettings settings)
        {
            if (settings == null) return false;
            return settings.NeedsPlayerInteraction()
                   && settings.AutoAccept
                   && settings.GetSelectionCount(default) == 1;
        }
    }
}
