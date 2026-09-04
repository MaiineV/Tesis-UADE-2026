using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Filtro común de destino de teleport: una celda caminable pero SIN edges (isla de
    /// 1 celda en el NavGraph bakeado — tile suelto bajo una decoración, esquina huérfana
    /// de una sala) es una trampa: el que teleporta ahí no puede volver a salir caminando.
    /// El planner de piso nunca genera islas; las que existen son gajes del autorado de
    /// salas, así que se descartan como destino en vez de confiar en el rebake.
    /// </summary>
    public static class TeleportCellFilter
    {
        /// <summary>
        /// True si <paramref name="coord"/> es una isla sin salida. Con grafo null o
        /// stub vacío (tests) nada es isla — el stub "infinito" no tiene edges y
        /// vetarlo todo rompería los fakes de EditMode.
        /// </summary>
        public static bool IsStrandedCell(IGridManager grid, GridCoord coord)
        {
            var graph = grid?.Graph;
            if (graph == null || graph.IsEmpty) return false;
            if (!graph.HasNode(coord)) return false;

            foreach (var _ in graph.GetNeighbors(coord)) return false;
            return true;
        }
    }
}
