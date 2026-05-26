using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Implementación default de <see cref="IMovementService"/>.
    /// <see cref="GetReachableTiles"/> usa BFS (range query); <see cref="FindPath"/> usa
    /// A* con heurística Manhattan (point-to-point en 4-neighborhood, costo uniforme).
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

                foreach (var edge in _grid.Graph.GetNeighbors(current))
                {
                    var n = edge.To;
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
            if (_grid.IsOccupied(to)) return new List<GridCoord>();

            // A* con heurística Manhattan. Costo de step = 1 (uniforme, 4-neighborhood).
            // En FP las salas son chicas (<100 nodos) así que un open-set como List con
            // búsqueda lineal del mínimo es más simple que una priority queue y
            // suficientemente rápido. Si crece el grid hay que migrar a heap binario.
            var cameFrom = new Dictionary<GridCoord, GridCoord>();
            var gScore = new Dictionary<GridCoord, int> { [from] = 0 };
            var fScore = new Dictionary<GridCoord, int> { [from] = from.Manhattan(to) };
            var open = new List<GridCoord> { from };
            var openSet = new HashSet<GridCoord> { from };

            while (open.Count > 0)
            {
                // Pop el de menor f. O(n) en open — aceptable para FP scale.
                int bestIdx = 0;
                int bestF = fScore[open[0]];
                for (int i = 1; i < open.Count; i++)
                {
                    int f = fScore[open[i]];
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }
                var current = open[bestIdx];
                open.RemoveAt(bestIdx);
                openSet.Remove(current);

                if (current == to) return ReconstructPath(cameFrom, current, from);

                foreach (var edge in _grid.Graph.GetNeighbors(current))
                {
                    var n = edge.To;
                    if (!_grid.IsWalkable(n)) continue;
                    // Tile ocupado bloquea el paso, salvo el destino (chequeado al inicio,
                    // así que llegar acá implica destino libre).
                    if (_grid.IsOccupied(n) && n != to) continue;

                    int tentativeG = gScore[current] + 1;
                    if (gScore.TryGetValue(n, out var existingG) && tentativeG >= existingG) continue;

                    cameFrom[n] = current;
                    gScore[n] = tentativeG;
                    fScore[n] = tentativeG + n.Manhattan(to);
                    if (openSet.Add(n)) open.Add(n);
                }
            }

            return new List<GridCoord>();
        }

        private static List<GridCoord> ReconstructPath(
            Dictionary<GridCoord, GridCoord> cameFrom, GridCoord goal, GridCoord start)
        {
            var path = new List<GridCoord> { goal };
            var cursor = goal;
            while (cursor != start)
            {
                cursor = cameFrom[cursor];
                path.Add(cursor);
            }
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
