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
    }
}
