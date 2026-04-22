using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Implementación default de <see cref="IMovementService"/> — BFS 4-neighborhood.
    /// TECHNICAL.md §17.§B.
    /// </summary>
    public sealed class MovementService : IMovementService
    {
        private readonly IGridManager _grid;

        public MovementService(IGridManager grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

        public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
        {
            var result = new List<GridCoord>();
            if (range < 0) return result;

            var visited = new Dictionary<GridCoord, int> { [origin] = 0 };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int distance = visited[current];

                if (distance > 0 || includeOrigin) result.Add(current);
                if (distance == range) continue;

                foreach (var n in current.Neighbors4())
                {
                    if (visited.ContainsKey(n)) continue;
                    if (!_grid.IsWalkable(n)) continue;
                    if (_grid.IsOccupied(n)) continue;

                    visited[n] = distance + 1;
                    queue.Enqueue(n);
                }
            }

            return result;
        }

        public List<GridCoord> FindPath(GridCoord from, GridCoord to)
        {
            if (from == to) return new List<GridCoord> { from };
            if (!_grid.IsWalkable(to)) return new List<GridCoord>();
            if (_grid.IsOccupied(to) && !(_grid.TryGetOccupant(to, out _)))
            {
                // ocupado pero el grid manager no puede resolver occupant — conservador, falla
                return new List<GridCoord>();
            }

            var cameFrom = new Dictionary<GridCoord, GridCoord> { [from] = from };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(from);
            bool reached = false;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to) { reached = true; break; }

                foreach (var n in current.Neighbors4())
                {
                    if (cameFrom.ContainsKey(n)) continue;
                    if (!_grid.IsWalkable(n)) continue;

                    // Permitir paso por tile destino aunque esté ocupado por sí mismo; otros ocupantes bloquean
                    if (_grid.IsOccupied(n) && n != to) continue;
                    if (_grid.IsOccupied(n) && n == to) continue;

                    cameFrom[n] = current;
                    queue.Enqueue(n);
                }
            }

            if (!reached) return new List<GridCoord>();

            var path = new List<GridCoord>();
            var cursor = to;
            while (cursor != from)
            {
                path.Add(cursor);
                cursor = cameFrom[cursor];
            }
            path.Add(from);
            path.Reverse();
            return path;
        }

        public bool Move(Guid entity, GridCoord destination)
        {
            if (!_grid.TryGetPosition(entity, out var from))
            {
                Debug.LogWarning($"[MovementService] Move: entidad {entity} no registrada en grid.");
                return false;
            }
            if (from == destination) return true;

            var path = FindPath(from, destination);
            if (path.Count == 0) return false;

            if (!_grid.Move(entity, destination)) return false;

            OnEntityMoved?.Invoke(entity, from, destination, path);
            return true;
        }
    }
}
