using System;
using System.Collections.Generic;
using Rollgeon.Combat.Threat;
using Rollgeon.Tiles;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Resuelve la casilla que cuenta como "centro de la sala". Único punto de esa matemática:
    /// la comparte <see cref="Rollgeon.Combat.AI.Decisions.AINode_TeleportToRoomCenter"/> (a dónde
    /// reubicarse) con <see cref="Rollgeon.PreConditions.Concretes.PcOwnerAtRoomCenter"/> (si ya
    /// se está ahí). Si divergieran, el gate de "no está en el centro" se abriría en una casilla a
    /// la que el teleport nunca lleva: el salto no movería nada y el ataque se gastaría mudo.
    /// </summary>
    public static class RoomCenterResolver
    {
        /// <summary>
        /// Centro del bounding box de la sala si está usable, y si no la casilla usable más cercana.
        /// <c>false</c> sólo si la sala no ofrece ninguna.
        /// </summary>
        /// <param name="avoidHarmfulTiles">
        /// Descarta las casillas que hacen daño (<see cref="HarmfulTileQuery"/>). Es preferencia y no
        /// requisito: si el filtro deja la sala sin candidatas se resuelve igual sin él, porque un
        /// <c>false</c> acá aborta la Sequence del turno del jefe.
        /// </param>
        /// <remarks>
        /// <para>
        /// "Usable" = caminable y libre, con la propia casilla de <paramref name="selfGuid"/> contando
        /// como libre: descartarla mandaría a un salto lateral cuando ya se estaba lo más cerca del
        /// centro que hay.
        /// </para>
        /// <para>
        /// El filtro vive acá y no en el nodo que reubica porque el gate que pregunta "¿ya está en el
        /// centro?" resuelve por esta misma función: leyendo distinto, el jefe parado en la casilla
        /// limpia de al lado abriría el gate, el teleport resolvería la casilla donde ya está y el
        /// ataque se gastaría sin salto.
        /// </para>
        /// </remarks>
        public static bool TryResolve(
            IGridManager grid, Guid selfGuid, GridCoord selfCoord, out GridCoord destination,
            bool avoidHarmfulTiles = false)
        {
            destination = selfCoord;
            if (grid == null) return false;

            if (avoidHarmfulTiles && TryPick(grid, selfGuid, selfCoord, true, out destination))
                return true;

            return TryPick(grid, selfGuid, selfCoord, false, out destination);
        }

        private static bool TryPick(
            IGridManager grid, Guid selfGuid, GridCoord selfCoord, bool skipHarmful,
            out GridCoord destination)
        {
            destination = selfCoord;

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito". Materializado
            // porque se recorre dos veces (bounds + pick).
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

            // División entera: en un lado de largo par el centro cae en la casilla de abajo/izquierda.
            // Arbitrario pero estable — el resultado es siempre la misma casilla, no alterna entre
            // las dos del medio de turno a turno.
            var center = new GridCoord((minX + maxX) / 2, (minY + maxY) / 2);

            bool found = false;
            int bestToCenter = int.MaxValue;
            int bestFromSelf = int.MaxValue;
            foreach (var c in tiles)
            {
                if (!IsFreeFor(grid, c, selfGuid)) continue;
                if (skipHarmful && HarmfulTileQuery.IsHarmfulAt(c)) continue;

                int toCenter = c.Manhattan(center);
                int fromSelf = c.Manhattan(selfCoord);
                if (found && !IsBetter(c, toCenter, fromSelf, destination, bestToCenter, bestFromSelf))
                    continue;

                destination = c;
                bestToCenter = toCenter;
                bestFromSelf = fromSelf;
                found = true;
            }

            return found;
        }

        private static bool IsFreeFor(IGridManager grid, GridCoord coord, Guid selfGuid)
        {
            if (!grid.IsOccupied(coord)) return true;
            return grid.TryGetOccupant(coord, out var occupant) && occupant == selfGuid;
        }

        /// <remarks>
        /// Cercanía al centro primero, empates por el salto más corto y después por menor (Y, X), para
        /// que el destino no dependa del orden en que el grafo horneado enumera sus nodos.
        /// </remarks>
        private static bool IsBetter(
            GridCoord candidate, int toCenter, int fromSelf,
            GridCoord best, int bestToCenter, int bestFromSelf)
        {
            if (toCenter != bestToCenter) return toCenter < bestToCenter;
            if (fromSelf != bestFromSelf) return fromSelf < bestFromSelf;
            if (candidate.Y != best.Y) return candidate.Y < best.Y;
            return candidate.X < best.X;
        }
    }
}
