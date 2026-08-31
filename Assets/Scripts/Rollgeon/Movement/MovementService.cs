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
    public sealed class MovementService : IMovementService, IPathedMovementService, IMoveTruncationService
    {
        private readonly IGridManager _grid;

        // Un solo filtro (el motor de Casillas Especiales). Ver IMovementPathFilter.
        private IMovementPathFilter _pathFilter;

        public MovementService(IGridManager grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

        public event Action<Guid, GridCoord, GridCoord> OnEntityTeleported;

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

            // El filtro puede truncar el path (Hielo/Portal terminan el movimiento ahí);
            // el evento anuncia SOLO lo realmente caminado.
            var effective = ApplyFilter(entity, path);
            if (effective.Count < 2) return false;

            var target = effective[effective.Count - 1];
            if (!_grid.Move(entity, target)) return false;

            OnEntityMoved?.Invoke(entity, from, target, effective);
            return true;
        }

        // ======================================================================
        // IPathedMovementService
        // ======================================================================

        /// <inheritdoc />
        public void SetPathFilter(IMovementPathFilter filter) => _pathFilter = filter;

        /// <inheritdoc />
        public bool CommitPath(Guid entity, IReadOnlyList<GridCoord> path, bool applyPathFilter = false)
        {
            if (path == null || path.Count == 0) return false;
            if (!_grid.TryGetPosition(entity, out var from))
            {
                Debug.LogWarning($"[MovementService] CommitPath: entidad {entity} no registrada en grid.");
                return false;
            }
            if (path[0] != from) return false;

            var effective = applyPathFilter ? ApplyFilter(entity, path) : path;
            // Solo el origen: no hay nada que caminar — no-op válido, sin evento.
            if (effective.Count < 2) return effective.Count == 1;

            for (int i = 1; i < effective.Count; i++)
            {
                var step = effective[i];
                if (effective[i - 1].Manhattan(step) != 1) return false;
                if (!_grid.IsWalkable(step)) return false;
                if (_grid.IsOccupied(step)) return false;
            }

            var target = effective[effective.Count - 1];
            if (!_grid.Move(entity, target)) return false;

            OnEntityMoved?.Invoke(entity, from, target, effective);
            return true;
        }

        // ======================================================================
        // IMoveTruncationService
        // ======================================================================

        /// <inheritdoc />
        public event Action<Guid, GridCoord, GridCoord> OnEntityMoveTruncated;

        /// <inheritdoc />
        public bool TryTruncateMoveAt(Guid entity, GridCoord cell)
        {
            if (!_grid.TryGetPosition(entity, out var from))
            {
                Debug.LogWarning($"[MovementService] TryTruncateMoveAt: entidad {entity} no registrada en grid.");
                return false;
            }
            if (from == cell) return true;
            if (!_grid.IsWalkable(cell) || _grid.IsOccupied(cell)) return false;
            if (!_grid.Move(entity, cell)) return false;

            // Sin OnEntityMoved a propósito (ver doc de la interfaz).
            OnEntityMoveTruncated?.Invoke(entity, from, cell);
            return true;
        }

        /// <inheritdoc />
        public bool Teleport(Guid entity, GridCoord to)
        {
            if (!_grid.TryGetPosition(entity, out var from))
            {
                Debug.LogWarning($"[MovementService] Teleport: entidad {entity} no registrada en grid.");
                return false;
            }
            if (from == to) return true;
            if (!_grid.IsWalkable(to) || _grid.IsOccupied(to)) return false;
            if (!_grid.Move(entity, to)) return false;

            OnEntityTeleported?.Invoke(entity, from, to);
            return true;
        }

        private IReadOnlyList<GridCoord> ApplyFilter(Guid entity, IReadOnlyList<GridCoord> path)
        {
            if (_pathFilter == null) return path;
            return _pathFilter.Filter(entity, path) ?? path;
        }
    }
}
