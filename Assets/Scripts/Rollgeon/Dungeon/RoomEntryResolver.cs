using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Resuelve la casilla por la que se entra a una sala. Único punto de esa matemática: la
    /// comparte <see cref="PlayerRoomTransitioner"/> (dónde aparece el jugador) con
    /// <see cref="Rollgeon.Combat.Handoff.DefaultEnemySpawnResolver"/> (contra qué pared arranca
    /// el jefe). Si divergieran, el jefe quedaría opuesto a una puerta distinta de la que el
    /// jugador cruzó, que es justo el efecto que se quiso evitar.
    /// </summary>
    public static class RoomEntryResolver
    {
        /// <summary>
        /// Casilla interior de la puerta de entrada, o el spawn autorado del jugador si no hay
        /// puerta declarada. <c>false</c> sólo si la sala no ofrece ninguna de las dos.
        /// </summary>
        /// <param name="entryDirection">
        /// <see cref="IDungeonService.LastEntryDirection"/>. <c>null</c> en el spawn inicial y en
        /// los teleports por id, donde la sala no se entró por ninguna puerta.
        /// </param>
        public static bool TryResolve(
            IGridManager grid, RoomLayout layout, DoorDirection? entryDirection,
            out GridCoord entryCoord)
        {
            entryCoord = GridCoord.Zero;
            if (grid == null || layout == null) return false;

            if (entryDirection.HasValue)
            {
                var slot = layout.GetDoorSlot(entryDirection.Value);
                if (slot?.Anchor != null)
                {
                    // El anchor está en el borde de la sala (sobre la pared/puerta). La casilla
                    // que cuenta es la primera interior — la que se pisa al cruzar. Si no es
                    // walkable (layout raro), cae al anchor para no quedar afuera de la sala.
                    var anchorCoord = grid.WorldToGrid(slot.Anchor.position);
                    var interior = anchorCoord + entryDirection.Value.InwardOffset();
                    entryCoord = grid.IsWalkable(interior) ? interior : anchorCoord;
                    return true;
                }
            }

            if (layout.PlayerSpawnPoint != null)
            {
                entryCoord = grid.WorldToGrid(layout.PlayerSpawnPoint.position);
                return true;
            }

            return false;
        }
    }
}
