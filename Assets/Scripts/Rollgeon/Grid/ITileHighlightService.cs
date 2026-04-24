using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    public interface ITileHighlightService
    {
        void RegisterTile(GridCoord coord, Renderer renderer);
        void UnregisterAll();
        void Highlight(IEnumerable<GridCoord> tiles, string style);
        void ClearAll();
    }
}
