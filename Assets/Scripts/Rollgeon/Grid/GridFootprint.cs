using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Rectángulo de celdas que ocupa una entidad: <c>ancho × alto</c> desde el ancla, que es
    /// la celda inferior-izquierda (min X, min Y). Misma semántica que
    /// <c>TileMarker.Footprint</c> / <c>NavGraphBaker.BlockerBounds</c>: min = ancla,
    /// tamaño = max(1, ·). (1,1) es el enemigo común y no cambia nada.
    /// </summary>
    public static class GridFootprint
    {
        public static readonly Vector2Int Unit = Vector2Int.one;

        public static Vector2Int Normalize(Vector2Int footprint)
            => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));

        public static bool IsUnit(Vector2Int footprint) => footprint.x <= 1 && footprint.y <= 1;

        /// <summary>Celdas cubiertas, fila por fila desde el ancla.</summary>
        public static IEnumerable<GridCoord> Cells(GridCoord anchor, Vector2Int footprint)
        {
            footprint = Normalize(footprint);
            for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                    yield return new GridCoord(anchor.X + x, anchor.Y + y);
        }

        /// <summary>
        /// Distancia Manhattan mínima entre dos rectángulos (celda más cercana de A a celda
        /// más cercana de B). 0 si se solapan; para dos 1×1 equivale a
        /// <see cref="GridCoord.Manhattan"/>. O(1) por eje — no itera celdas.
        /// </summary>
        public static int ManhattanDistance(GridCoord anchorA, Vector2Int fpA, GridCoord anchorB, Vector2Int fpB)
        {
            AxisGaps(anchorA, fpA, anchorB, fpB, out int dx, out int dy);
            return dx + dy;
        }

        /// <summary>Overload contra una celda única (footprint (1,1)).</summary>
        public static int ManhattanDistance(GridCoord anchorA, Vector2Int fpA, GridCoord b)
            => ManhattanDistance(anchorA, fpA, b, Unit);

        /// <summary>Distancia Chebyshev mínima entre dos rectángulos. Ver <see cref="ManhattanDistance(GridCoord, Vector2Int, GridCoord, Vector2Int)"/>.</summary>
        public static int ChebyshevDistance(GridCoord anchorA, Vector2Int fpA, GridCoord anchorB, Vector2Int fpB)
        {
            AxisGaps(anchorA, fpA, anchorB, fpB, out int dx, out int dy);
            return Mathf.Max(dx, dy);
        }

        /// <summary>Overload contra una celda única (footprint (1,1)).</summary>
        public static int ChebyshevDistance(GridCoord anchorA, Vector2Int fpA, GridCoord b)
            => ChebyshevDistance(anchorA, fpA, b, Unit);

        /// <summary>Separación por eje entre los intervalos de los dos rectángulos (0 = se tocan
        /// o solapan en ese eje).</summary>
        private static void AxisGaps(GridCoord anchorA, Vector2Int fpA, GridCoord anchorB, Vector2Int fpB,
            out int dx, out int dy)
        {
            fpA = Normalize(fpA);
            fpB = Normalize(fpB);
            dx = Mathf.Max(0, Mathf.Max(anchorB.X - (anchorA.X + fpA.x - 1), anchorA.X - (anchorB.X + fpB.x - 1)));
            dy = Mathf.Max(0, Mathf.Max(anchorB.Y - (anchorA.Y + fpA.y - 1), anchorA.Y - (anchorB.Y + fpB.y - 1)));
        }

        /// <summary>
        /// Desplazamiento desde el centro de la celda ancla hasta el centro del rectángulo,
        /// para que el pawn de un 2×2 quede en el medio de sus cuatro celdas.
        /// </summary>
        public static Vector3 CenterOffset(Vector2Int footprint, float tileSize)
        {
            footprint = Normalize(footprint);
            return new Vector3((footprint.x - 1) * 0.5f * tileSize, 0f, (footprint.y - 1) * 0.5f * tileSize);
        }
    }
}
