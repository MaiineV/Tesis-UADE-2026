using System;
using Rollgeon.Grid;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>
    /// Envoltorio de <see cref="IForcedMovementService.Push"/> para "atraer hacia" (Garfio):
    /// frena UNA celda antes del ancla — el motor de empuje ya sabe frenar en obstáculos, así
    /// que acá solo hace falta calcular la dirección y la distancia correctas.
    /// </summary>
    public static class PullResolver
    {
        /// <summary>
        /// Empuja a <paramref name="entity"/> hacia <paramref name="anchor"/>, cardinal dominante
        /// (misma fila/columna por construcción del caller), distancia = <c>min(maxTiles,
        /// manhattan-1)</c> para terminar adyacente y no encima. <c>tiles ≤ 0</c> (ya adyacente o
        /// más cerca) es no-op.
        /// </summary>
        public static ForcedMoveResult PullToward(IForcedMovementService forced, IGridManager grid, Guid entity,
            GridCoord anchor, int maxTiles, Guid sourceId)
        {
            if (forced == null || grid == null || !grid.TryGetPosition(entity, out var entityCoord))
                return new ForcedMoveResult(default, 0, ForcedMoveStop.CompletedDistance, false);

            int tiles = Math.Min(maxTiles, entityCoord.Manhattan(anchor) - 1);
            if (tiles <= 0)
                return new ForcedMoveResult(entityCoord, 0, ForcedMoveStop.CompletedDistance, false);

            var dir = CardinalExtensions.FromDelta(entityCoord, anchor);
            return forced.Push(entity, dir, tiles, sourceId);
        }
    }
}
