using System.Collections.Generic;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Query compartida de "celda segura para reubicar a alguien acá" (Probability Drive,
    /// reacomodos de jefes futuros): unifica los cuatro filtros que hoy vive cada caller por
    /// separado (walkable, libre, dañina, telegrafiada) más Portal, que ningún otro filtro cubre.
    /// </summary>
    public static class SafeTileQuery
    {
        /// <summary>
        /// <c>true</c> si <paramref name="c"/> es walkable, está libre, no tiene una casilla
        /// dañina, no es un Portal, y no está telegrafiada. Servicios <c>null</c> degradan a
        /// "sin ese filtro" (nunca a "todo inseguro").
        /// </summary>
        public static bool IsSafe(GridCoord c, IGridManager grid, ISpecialTileService tiles,
            IThreatenedAreaService threats)
        {
            if (grid == null) return false;
            if (!grid.InBounds(c) || !grid.IsWalkable(c)) return false;
            if (!grid.IsFree(c)) return false;
            if (HarmfulTileQuery.IsHarmfulAt(c)) return false;
            if (IsPortal(c, tiles)) return false;
            if (threats != null && threats.IsThreatened(c)) return false;

            return true;
        }

        private static bool IsPortal(GridCoord c, ISpecialTileService tiles)
        {
            if (tiles == null) return false;
            if (!tiles.TryGetTileAt(c, out var info) || info.Definition == null) return false;
            return info.Definition.TileType == SpecialTileType.Portal;
        }

        /// <summary>
        /// Celdas seguras (<see cref="IsSafe"/>) a distancia Manhattan de <paramref name="center"/>
        /// en <c>[rMin, rMax]</c>. Orden determinístico row-major (Y ascendente, X ascendente) —
        /// el caller que necesite azar aplica su propio shuffle sobre el resultado.
        /// </summary>
        public static List<GridCoord> CollectRing(GridCoord center, int rMin, int rMax, IGridManager grid,
            ISpecialTileService tiles, IThreatenedAreaService threats)
        {
            var result = new List<GridCoord>();
            if (grid == null || rMin > rMax) return result;

            for (int y = center.Y - rMax; y <= center.Y + rMax; y++)
            {
                for (int x = center.X - rMax; x <= center.X + rMax; x++)
                {
                    var c = new GridCoord(x, y);
                    int dist = center.Manhattan(c);
                    if (dist < rMin || dist > rMax) continue;
                    if (IsSafe(c, grid, tiles, threats)) result.Add(c);
                }
            }

            return result;
        }
    }
}
