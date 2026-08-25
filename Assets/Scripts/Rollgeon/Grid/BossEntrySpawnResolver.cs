using System.Collections.Generic;
using Rollgeon.Combat.Threat;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Resuelve dónde arranca un jefe: contra la pared opuesta a la puerta por la que se entró,
    /// unas casillas adentro y alineado con el eje de esa puerta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es en runtime y no una celda autorada porque las salas de jefe tienen las cuatro puertas y
    /// cuál se abre lo decide la topología del piso. Una constante quedaría lejos de una puerta y
    /// encima de la de enfrente.
    /// </para>
    /// <para>
    /// Espeja a <see cref="RoomCenterResolver"/> —mismo bounding box, misma noción de "usable",
    /// mismo desempate estable— para que las dos matemáticas de posición de la sala se lean igual.
    /// </para>
    /// </remarks>
    public static class BossEntrySpawnResolver
    {
        /// <summary>
        /// Vecinos caminables mínimos de la casilla elegida. Es el mismo mínimo que valida el
        /// autorado en <c>BossRoomBuilder.MinBossAdjacency</c>: el jugador pega a distancia 1, y
        /// arrancar con una sola casilla libre al lado convierte la apertura en un cuello de
        /// botella. Se relaja antes de fallar, porque una posición peor es mejor que ninguna.
        /// </summary>
        public const int MinAdjacency = 2;

        /// <summary>
        /// <c>false</c> si la sala no ofrece ninguna casilla usable — ahí el llamador se queda con
        /// la celda autorada.
        /// </summary>
        /// <param name="wallInset">Casillas hacia adentro desde la pared opuesta. 0 la pega a la
        /// pared y le saca la fila de atrás para huir.</param>
        public static bool TryResolveAwayFromEntry(
            IGridManager grid, GridCoord entryCoord, int wallInset, out GridCoord destination)
        {
            destination = entryCoord;
            if (grid == null) return false;

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito", donde
            // IsWalkable dice que sí a todo. Materializado porque se recorre dos veces.
            var tiles = new List<GridCoord>(ThreatAreaShape.RoomTiles(grid));
            if (tiles.Count == 0) return false;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            var target = TargetCell(entryCoord, wallInset, minX, maxX, minY, maxY);

            // Dos pasadas y no un filtro con fallback en la misma: exigir los vecinos primero hace
            // que la relajación sólo entre en salas que de verdad no tienen ninguna casilla holgada.
            if (TryPick(grid, tiles, entryCoord, target, MinAdjacency, out destination)) return true;
            return TryPick(grid, tiles, entryCoord, target, 0, out destination);
        }

        /// <remarks>
        /// La pared de entrada es la más cercana a <paramref name="entryCoord"/>. El objetivo cae
        /// sobre la de enfrente, y conserva la otra coordenada de la puerta: así queda enfilado con
        /// ella y no en una esquina, que es lo que separa "lejos" de "arrinconado".
        /// </remarks>
        private static GridCoord TargetCell(
            GridCoord entryCoord, int wallInset, int minX, int maxX, int minY, int maxY)
        {
            int toWest = entryCoord.X - minX;
            int toEast = maxX - entryCoord.X;
            int toSouth = entryCoord.Y - minY;
            int toNorth = maxY - entryCoord.Y;

            int horizontal = toWest < toEast ? toWest : toEast;
            int vertical = toSouth < toNorth ? toSouth : toNorth;

            // Empate entre ejes: el vertical. Arbitrario pero estable — el resultado no puede
            // alternar entre dos casillas de una entrada a la otra.
            if (vertical <= horizontal)
            {
                int y = toSouth <= toNorth ? maxY - wallInset : minY + wallInset;
                return new GridCoord(entryCoord.X, Clamp(y, minY, maxY));
            }

            int x = toWest <= toEast ? maxX - wallInset : minX + wallInset;
            return new GridCoord(Clamp(x, minX, maxX), entryCoord.Y);
        }

        /// <remarks>
        /// Cercanía al objetivo primero, empates por la casilla <b>más lejos</b> de la entrada y
        /// después por menor (Y, X), para que no dependa del orden en que el grafo horneado
        /// enumera sus nodos.
        /// </remarks>
        private static bool TryPick(
            IGridManager grid, List<GridCoord> tiles, GridCoord entryCoord, GridCoord target,
            int minAdjacency, out GridCoord destination)
        {
            destination = entryCoord;

            bool found = false;
            int bestToTarget = int.MaxValue;
            int bestFromEntry = int.MinValue;

            foreach (var c in tiles)
            {
                if (c == entryCoord) continue;
                if (grid.IsOccupied(c)) continue;
                if (minAdjacency > 0 && WalkableNeighbors(grid, c) < minAdjacency) continue;

                int toTarget = c.Manhattan(target);
                int fromEntry = c.Manhattan(entryCoord);
                if (found && !IsBetter(c, toTarget, fromEntry, destination, bestToTarget, bestFromEntry))
                    continue;

                destination = c;
                bestToTarget = toTarget;
                bestFromEntry = fromEntry;
                found = true;
            }

            return found;
        }

        private static bool IsBetter(
            GridCoord candidate, int toTarget, int fromEntry,
            GridCoord best, int bestToTarget, int bestFromEntry)
        {
            if (toTarget != bestToTarget) return toTarget < bestToTarget;
            if (fromEntry != bestFromEntry) return fromEntry > bestFromEntry;
            if (candidate.Y != best.Y) return candidate.Y < best.Y;
            return candidate.X < best.X;
        }

        /// <remarks>
        /// Cuenta sobre las aristas del grafo y no sobre <c>IsWalkable</c>: un nodo caminable puede
        /// tener grado 0 —una isla del NavGraph— y ahí el jefe arranca encerrado.
        /// </remarks>
        private static int WalkableNeighbors(IGridManager grid, GridCoord coord)
        {
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) return 0;

            int count = 0;
            foreach (var _ in graph.GetNeighbors(coord)) count++;
            return count;
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;
    }
}
