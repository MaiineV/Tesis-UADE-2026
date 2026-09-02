using System;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Línea de visión entre dos celdas del grid, en cualquier ángulo (no solo ortogonal o
    /// diagonal de 45°) — algoritmo de Bresenham. Complemento del chequeo de línea recta que ya
    /// usa <c>PcTargetInRange.RequireLineOfSight</c> (ortogonal/diagonal únicamente, GDD Sniper):
    /// ese sirve para atacantes que disparan en línea recta; este es para atacantes
    /// omnidireccionales (ej. AoE en rango, GDD Ranged Kiter) que igual necesitan que la celda
    /// puntual del target no esté tapada, sin exigir alineación.
    /// </summary>
    public static class GridLineOfSight
    {
        /// <summary>
        /// True si ninguna celda ESTRICTAMENTE intermedia entre <paramref name="from"/> y
        /// <paramref name="to"/> bloquea (no caminable, u ocupada por alguien que no sea
        /// <paramref name="ignoreA"/>/<paramref name="ignoreB"/>). Ni el origen ni el destino se
        /// evalúan — el propio target nunca se bloquea a sí mismo.
        /// </summary>
        /// <remarks>
        /// Bresenham simple, no la variante "simétrica" — en diagonales muy cerradas puede dar
        /// A→B ligeramente distinto de B→A. Aceptado a propósito: el costo de la variante
        /// simétrica no se justifica para el uso actual (ninguna ficha del GDD depende de esa
        /// simetría exacta); si algún día hace falta, se cambia acá sin tocar los callers.
        /// </remarks>
        /// <remarks>
        /// Sin corte de esquina (BUG de playtest): un paso de Bresenham puede mover X e Y a la
        /// vez (paso diagonal real, ej. (5,-4)→(4,-5)) pasando JUSTO por la esquina de un
        /// obstáculo sin pisar ninguna de sus celdas — el enemigo "veía" a través del borde de
        /// una mesa. Con las dos celdas que forman esa esquina — (x_prev,y) y (x,y_prev), las
        /// ortogonalmente adyacentes al paso — si CUALQUIERA de las dos está bloqueada, el corte
        /// no vale: mismo criterio que el pathing "no cortar esquinas" de la mayoría de los
        /// juegos de grilla.
        /// </remarks>
        public static bool HasClearLine(IGridManager grid, GridCoord from, GridCoord to, Guid ignoreA, Guid ignoreB)
        {
            if (grid == null) return false;
            if (from.Equals(to)) return true;

            int x0 = from.X, y0 = from.Y;
            int x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int x = x0, y = y0;

            while (x != x1 || y != y1)
            {
                int prevX = x, prevY = y;
                int e2 = 2 * err;
                bool steppedX = false, steppedY = false;
                if (e2 >= dy) { err += dy; x += sx; steppedX = true; }
                if (e2 <= dx) { err += dx; y += sy; steppedY = true; }

                // Paso diagonal real (X e Y cambiaron juntos): no vale cortar la esquina si
                // cualquiera de las dos celdas que la forman está bloqueada, aunque la celda
                // diagonal en sí esté libre.
                if (steppedX && steppedY)
                {
                    var flankA = new GridCoord(x, prevY);
                    var flankB = new GridCoord(prevX, y);
                    if (!IsClearOrEndpoint(grid, flankA, x1, y1, ignoreA, ignoreB)) return false;
                    if (!IsClearOrEndpoint(grid, flankB, x1, y1, ignoreA, ignoreB)) return false;
                }

                if (x == x1 && y == y1) break; // llegamos al target — su celda nunca bloquea

                var c = new GridCoord(x, y);
                if (!grid.IsWalkable(c)) return false;
                if (grid.TryGetOccupant(c, out var occupant) && occupant != ignoreA && occupant != ignoreB)
                    return false;
            }

            return true;
        }

        /// <summary>Celda de flanco de un corte diagonal: bloquea igual que cualquier celda de
        /// la línea, salvo que sea justo el target (su celda nunca bloquea, aunque el paso
        /// diagonal la haya usado de flanco en vez de pisarla directo).</summary>
        private static bool IsClearOrEndpoint(IGridManager grid, GridCoord c, int targetX, int targetY, Guid ignoreA, Guid ignoreB)
        {
            if (c.X == targetX && c.Y == targetY) return true;
            if (!grid.IsWalkable(c)) return false;
            if (grid.TryGetOccupant(c, out var occupant) && occupant != ignoreA && occupant != ignoreB) return false;
            return true;
        }
    }
}
