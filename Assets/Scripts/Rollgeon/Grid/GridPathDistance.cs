using System;
using System.Collections.Generic;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Mapa de costos de CAMINO real (Dijkstra sobre <see cref="IGridManager.Graph"/>, sin tope de
    /// pasos) desde una celda origen — para puntuar candidatos de reposicionamiento por cuánto
    /// realmente acortan la ruta hasta un objetivo, no por distancia Manhattan en línea recta.
    /// </summary>
    /// <remarks>
    /// Nace de <c>AINode_MoveToAlign</c> (Sniper): contra un obstáculo ancho, Manhattan en línea
    /// recta no distingue "seguir derecho contra la pared" de "empezar a bordearla" — ambos
    /// candidatos miden lo mismo en línea recta aunque uno tenga camino real más corto. La
    /// distancia de camino sí los distingue, así que el reposicionamiento converge en vez de
    /// oscilar o quedarse pegado al obstáculo. Reusado por cualquier nodo de movimiento que
    /// necesite el mismo criterio (ej. <c>AINode_MoveToLineOfSight</c>, Ranged Kiter) y por la
    /// fase de desbloqueo de <c>AIPathPlanner</c>.
    /// </remarks>
    public static class GridPathDistance
    {
        /// <summary>
        /// Sobrecosto de ATRAVESAR una celda ocupada por un tercero. Un desvío de hasta este
        /// largo se prefiere antes que contar con que el ocupante se corra.
        /// </summary>
        public const int DefaultOccupantCost = 4;

        /// <summary>
        /// Costo de camino desde <paramref name="from"/> a cada celda alcanzable, ignorando el
        /// ocupante de <paramref name="ignoreA"/>/<paramref name="ignoreB"/> (no deberían
        /// bloquearse la ruta el uno al otro — típicamente el propio mover y su target).
        /// Solo para PUNTUAR candidatos: no respeta ningún tope de pasos de turno, eso lo sigue
        /// imponiendo el conjunto de candidatos alcanzables del caller.
        /// </summary>
        public static Dictionary<GridCoord, int> ComputeFrom(IGridManager grid, GridCoord from, Guid ignoreA, Guid ignoreB)
            => ComputeFrom(grid, from, ignoreA, ignoreB, DefaultOccupantCost);

        /// <summary>
        /// Igual que la sobrecarga de 4 argumentos, con el sobrecosto de ocupante explícito.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Los ocupantes ya no son pared (BUG de playtest).</b> Antes esto era un BFS donde
        /// cualquier ocupante cortaba la expansión: si un aliado tapaba el único corredor hacia el
        /// jugador, el enemigo de atrás quedaba FUERA del mapa, su nodo de movimiento no encontraba
        /// ningún candidato puntuable y se congelaba para siempre — se destrababa recién cuando el
        /// jugador se movía y cambiaba el origen del mapa. Los ocupantes son transitorios (se
        /// mueven todos los turnos), así que tratarlos como muro permanente en una heurística de
        /// SCORING está mal: acá cuestan <paramref name="occupantCost"/> extra. Con desvío
        /// disponible la ruta los rodea; sin desvío el costo igual es finito y el enemigo avanza
        /// hacia la fila en vez de quedarse clavado. Sólo el terreno no caminable corta.
        /// </para>
        /// <para>
        /// <b>El valor es COSTO, no cantidad de tiles.</b> El orden sigue siendo monótono respecto
        /// de "qué tan lejos estoy por camino real" (que es lo único que usan los callers), pero la
        /// magnitud ya no se puede comparar contra un rango en tiles.
        /// </para>
        /// </remarks>
        public static Dictionary<GridCoord, int> ComputeFrom(IGridManager grid, GridCoord from,
            Guid ignoreA, Guid ignoreB, int occupantCost)
        {
            var cost = new Dictionary<GridCoord, int> { [from] = 0 };
            if (grid == null) return cost;

            if (occupantCost < 0) occupantCost = 0;

            // Label-correcting (relajación estilo Bellman-Ford sobre una cola FIFO): con costos
            // chicos y no negativos converge en pocas pasadas y evita el O(V²) de buscar el mínimo
            // linealmente — este mapa ahora se pide en cada tick de movimiento, no sólo al atascarse.
            var open = new Queue<GridCoord>();
            open.Enqueue(from);
            while (open.Count > 0)
            {
                var current = open.Dequeue();
                int d = cost[current];

                foreach (var edge in grid.Graph.GetNeighbors(current))
                {
                    var n = edge.To;
                    if (!grid.IsWalkable(n)) continue;

                    int step = 1;
                    if (grid.TryGetOccupant(n, out var occupant) && occupant != ignoreA && occupant != ignoreB)
                        step += occupantCost;

                    int next = d + step;
                    if (cost.TryGetValue(n, out var known) && known <= next) continue;

                    cost[n] = next;
                    open.Enqueue(n);
                }
            }

            return cost;
        }
    }
}
