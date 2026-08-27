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
