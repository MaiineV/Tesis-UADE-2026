using System;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    [Serializable]
    public class TargetRef
    {
        public GridCoord Coord;

        public TargetRef(GridCoord coord)
        {
            Coord = coord;
        }

        public static TargetRef At(GridCoord coord) => new TargetRef(coord);
    }
}
