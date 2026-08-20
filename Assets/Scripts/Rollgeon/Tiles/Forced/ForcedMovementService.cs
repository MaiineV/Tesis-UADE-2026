using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>
    /// Implementación de <see cref="IForcedMovementService"/>. Con
    /// <see cref="SpecialTileService"/> presente delega la cadena completa (triggers +
    /// continuaciones) en su motor; sin él degrada a un empuje "físico" puro que solo
    /// camina hasta el obstáculo — los sistemas suscriptos a <c>OnEntityMoved</c>
    /// (hazards de bosses, visuals) lo ven igual.
    /// </summary>
    public sealed class ForcedMovementService : IForcedMovementService, IPreloadableService
    {
        /// <summary>Después del SpecialTileService (79), junto a Threat/AI (80).</summary>
        public int Priority => 81;

        public void Register()
        {
            ServiceLocator.AddService<IForcedMovementService>(this, ServiceScope.Global);
            ServiceLocator.AddService<ForcedMovementService>(this, ServiceScope.Global);
        }

        /// <inheritdoc />
        public ForcedMoveResult Push(Guid entity, Cardinal direction, int tiles, Guid sourceId)
        {
            if (entity == Guid.Empty || tiles <= 0)
                return new ForcedMoveResult(default, 0, ForcedMoveStop.CompletedDistance, false);

            if (ServiceLocator.TryGetService<SpecialTileService>(out var tileService) && tileService != null)
                return tileService.RunForcedChain(entity, direction, tiles);

            return PlainPush(entity, direction, tiles);
        }

        /// <summary>Fallback sin casillas especiales: pasos crudos hasta chocar.</summary>
        private static ForcedMoveResult PlainPush(Guid entity, Cardinal direction, int tiles)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
                return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false);
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement)
                || !(movement is IPathedMovementService pathed))
                return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false);
            if (!grid.TryGetPosition(entity, out var current))
                return new ForcedMoveResult(default, 0, ForcedMoveStop.Obstacle, false);

            var path = new List<GridCoord> { current };
            var cursor = current;
            var stop = ForcedMoveStop.CompletedDistance;
            for (int i = 0; i < tiles; i++)
            {
                var next = direction.Step(cursor);
                if (!grid.IsWalkable(next) || grid.IsOccupied(next)) { stop = ForcedMoveStop.Obstacle; break; }
                path.Add(next);
                cursor = next;
            }

            if (path.Count >= 2) pathed.CommitPath(entity, path);
            return new ForcedMoveResult(cursor, path.Count - 1, stop, false);
        }
    }
}
