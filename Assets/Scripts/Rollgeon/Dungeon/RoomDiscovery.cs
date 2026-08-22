using System;
using System.Collections.Generic;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Reglas de fog of war del piso (#158), compartidas entre el floor view
    /// (<see cref="FloorShellVisibilityController"/>) y el minimapa del HUD.
    /// Única fuente de verdad: si cambia la regla de descubrimiento, cambia acá.
    /// </summary>
    public static class RoomDiscovery
    {
        public static bool IsVisited(Guid id, IReadOnlyDictionary<Guid, RoomInstance> rooms)
            => rooms != null && rooms.TryGetValue(id, out var room) && room != null && room.Visited;

        /// <summary>
        /// Descubierta = visitada O vecina conectada por puerta a una sala visitada.
        /// La adyacencia sale de <see cref="RoomInstance.Connections"/> (solo conexiones reales).
        /// </summary>
        public static bool IsDiscovered(Guid id, IReadOnlyDictionary<Guid, RoomInstance> rooms)
        {
            if (rooms == null || !rooms.TryGetValue(id, out var room) || room == null) return false;
            if (room.Visited) return true;
            foreach (var neighborId in room.Connections.Values)
                if (rooms.TryGetValue(neighborId, out var neighbor) && neighbor != null && neighbor.Visited)
                    return true;
            return false;
        }
    }
}
