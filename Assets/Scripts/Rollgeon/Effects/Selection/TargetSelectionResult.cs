using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    public class TargetSelectionResult
    {
        public bool WasCompleted;
        public bool WasCancelled;
        public bool WasSkipped;
        public List<TargetRef> SelectedTargets;

        public GridCoord? FirstSelectedCoord
        {
            get
            {
                if (SelectedTargets == null || SelectedTargets.Count == 0) return null;
                return SelectedTargets[0]?.Coord;
            }
        }

        /// <summary>
        /// Azúcar — primera celda seleccionada. Devuelve <c>false</c> si no hay targets
        /// o si el primer <see cref="TargetRef"/> no trae <see cref="TargetRef.Cell"/>.
        /// </summary>
        public bool TryGetFirstSelectedCell(out Rollgeon.Grid.GridCoord cell)
        {
            cell = default;
            if (SelectedTargets == null || SelectedTargets.Count == 0) return false;
            var first = SelectedTargets[0];
            if (first == null || !first.HasCell) return false;
            cell = first.Cell;
            return true;
        }
    }
}
