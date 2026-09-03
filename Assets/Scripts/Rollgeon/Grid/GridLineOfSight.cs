using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Línea de visión entre dos celdas del grid, en cualquier ángulo (no solo ortogonal o
    /// diagonal de 45°) — algoritmo de Bresenham. Es LA línea de visión del juego: la exigen
    /// todos los gates de ataque enemigo (<c>PcTargetInRange</c>, <c>PCEntityInRange</c>, los
    /// auto-gates tipo <c>AINode_RangedShot.CanFire</c>), el filtrado de telegraphs dirigidos y
    /// el overlay de alcance. Sobre pares alineados camina exactamente la línea recta.
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

            // Grafo vacío = el stub infinito de los tests (misma convención que
            // NavGraph.InBounds, que ahí devuelve true a todo): sin terreno cargado no hay
            // paredes y solo bloquean los ocupantes.
            bool hasTerrain = grid.Graph != null && !grid.Graph.IsEmpty;

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
                    if (!IsClearOrEndpoint(grid, flankA, x1, y1, ignoreA, ignoreB, hasTerrain)) return false;
                    if (!IsClearOrEndpoint(grid, flankB, x1, y1, ignoreA, ignoreB, hasTerrain)) return false;
                }

                if (x == x1 && y == y1) break; // llegamos al target — su celda nunca bloquea

                var c = new GridCoord(x, y);
                if (hasTerrain && !grid.IsWalkable(c)) return false;
                if (Blocks(grid, c, ignoreA, ignoreB)) return false;
            }

            return true;
        }

        /// <summary>
        /// Deja en <paramref name="tiles"/> solo las celdas con línea limpia desde
        /// <paramref name="origin"/>. La celda del propio origen (si está en el set) se conserva.
        /// In-place para no alocar: los llamadores (marcado de telegraphs) ya son dueños del set.
        /// </summary>
        public static void FilterVisible(IGridManager grid, GridCoord origin,
                                         HashSet<GridCoord> tiles, Guid ignoreA, Guid ignoreB)
        {
            if (grid == null || tiles == null || tiles.Count == 0) return;
            tiles.RemoveWhere(tile => !HasClearLine(grid, origin, tile, ignoreA, ignoreB));
        }

        /// <summary>Celda de flanco de un corte diagonal: bloquea igual que cualquier celda de
        /// la línea, salvo que sea justo el target (su celda nunca bloquea, aunque el paso
        /// diagonal la haya usado de flanco en vez de pisarla directo).</summary>
        private static bool IsClearOrEndpoint(IGridManager grid, GridCoord c, int targetX, int targetY, Guid ignoreA, Guid ignoreB, bool hasTerrain)
        {
            if (c.X == targetX && c.Y == targetY) return true;
            if (hasTerrain && !grid.IsWalkable(c)) return false;
            if (Blocks(grid, c, ignoreA, ignoreB)) return false;
            return true;
        }

        /// <summary>
        /// <c>true</c> si la celda tiene un ocupante que corta la línea. Ni <paramref name="ignoreA"/>
        /// (el atacante) ni <paramref name="ignoreB"/> (el target) bloquean nunca; tampoco un
        /// ALIADO del atacante — feedback de playtest: varios enemigos amontonados se tapaban
        /// el tiro entre ellos y generaba situaciones raras (el jugador veía a un enemigo
        /// "esperando" sin motivo visible porque otro le tapaba la línea).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Cuando <paramref name="ignoreA"/> e <paramref name="ignoreB"/> son ALIADOS entre sí (ej.
        /// un Healer chequeando si ve a su aliado herido para curarlo), NINGÚN ocupante bloquea —
        /// ni siquiera el jugador. Sin esta regla el jugador podía pararse justo en el medio y
        /// "tapar" la cura sin que el Healer supiera reaccionar (el fallback de movimiento no sabe
        /// de LoS, así que el enemigo quedaba trabado para siempre en vez de reposicionarse) —
        /// decisión explícita: el jugador nunca bloquea LoS entre dos enemigos aliados entre sí,
        /// sólo el terreno lo hace. Fuera de ese caso (ej. un enemigo apuntándole al jugador), el
        /// jugador sigue bloqueando como cualquier ocupante.
        /// </para>
        /// </remarks>
        private static bool Blocks(IGridManager grid, GridCoord c, Guid ignoreA, Guid ignoreB)
        {
            if (!grid.TryGetOccupant(c, out var occupant)) return false;
            if (occupant == ignoreA || occupant == ignoreB) return false;

            if (ignoreA != Guid.Empty && ignoreB != Guid.Empty
                && ServiceLocator.TryGetService<IEntityQueryService>(out var query) && query != null)
            {
                // Chequeo entre dos aliados (ej. Healer → aliado a curar): nada vivo bloquea, ni
                // siquiera el jugador — sólo el terreno.
                if ((query.GetRelationship(ignoreA, ignoreB) & EntityFilterMask.Allies) != 0)
                    return false;

                // Regla original: un aliado del atacante nunca le tapa el tiro (enemigos amontonados).
                if ((query.GetRelationship(ignoreA, occupant) & EntityFilterMask.Allies) != 0)
                    return false;
            }

            return true;
        }
    }
}
