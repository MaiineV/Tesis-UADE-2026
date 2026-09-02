using System;
using System.Collections.Generic;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Mapa de distancias de CAMINO real (BFS sobre <see cref="IGridManager.Graph"/>, sin tope de
    /// pasos) desde una celda origen — para puntuar candidatos de reposicionamiento por cuánto
    /// realmente acortan la ruta hasta un objetivo, no por distancia Manhattan en línea recta.
    /// </summary>
    /// <remarks>
    /// Nace de <c>AINode_MoveToAlign</c> (Sniper): contra un obstáculo ancho, Manhattan en línea
    /// recta no distingue "seguir derecho contra la pared" de "empezar a bordearla" — ambos
    /// candidatos miden lo mismo en línea recta aunque uno tenga camino real más corto. La
    /// distancia de camino sí los distingue, así que el reposicionamiento converge en vez de
    /// oscilar o quedarse pegado al obstáculo. Reusado por cualquier nodo de movimiento que
    /// necesite el mismo criterio (ej. <c>AINode_MoveToLineOfSight</c>, Ranged Kiter).
    /// </remarks>
    public static class GridPathDistance
    {
        /// <summary>
        /// BFS de distancia (en tiles) desde <paramref name="from"/> a cada celda alcanzable,
        /// ignorando el ocupante de <paramref name="ignoreA"/>/<paramref name="ignoreB"/> (no
        /// deberían bloquearse la ruta el uno al otro — típicamente el propio mover y su target).
        /// Solo para PUNTUAR candidatos: no respeta ningún tope de pasos de turno, eso lo sigue
        /// imponiendo el conjunto de candidatos alcanzables del caller.
        /// </summary>
        public static Dictionary<GridCoord, int> ComputeFrom(IGridManager grid, GridCoord from, Guid ignoreA, Guid ignoreB)
        {
            var dist = new Dictionary<GridCoord, int> { [from] = 0 };
            if (grid == null) return dist;

            var queue = new Queue<GridCoord>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int d = dist[current];
                foreach (var edge in grid.Graph.GetNeighbors(current))
                {
                    var n = edge.To;
                    if (dist.ContainsKey(n)) continue;
                    if (!grid.IsWalkable(n)) continue;
                    if (grid.TryGetOccupant(n, out var occupant) && occupant != ignoreA && occupant != ignoreB) continue;

                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
