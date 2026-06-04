using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Resuelve las casillas "frente a puerta" de la sala activa — la primera celda
    /// interior delante de cada puerta abierta y conectada.
    /// <para>
    /// En Exploración estas casillas se pintan distinto (estilo "door") y, al
    /// seleccionarlas durante el movimiento, cruzan a la sala vecina. Reemplaza el
    /// viejo click directo sobre la puerta.
    /// </para>
    /// </summary>
    public static class DoorTileQuery
    {
        /// <summary>
        /// Mapa casilla-interior → dirección de la puerta, para cada puerta
        /// <see cref="DoorVisualState.Open"/> con vecino conectado en la sala actual.
        /// Vacío si no hay sala activa o ninguna puerta atravesable.
        /// </summary>
        public static Dictionary<GridCoord, DoorDirection> GetOpenDoorFrontTiles(
            IDungeonService dungeon, IGridManager grid)
        {
            var map = new Dictionary<GridCoord, DoorDirection>();

            var room = dungeon?.CurrentRoomInstance;
            if (room?.SpawnedPrefab == null || grid == null) return map;

            foreach (var door in room.SpawnedPrefab.GetComponentsInChildren<DoorController>())
            {
                // Solo puertas atravesables: abiertas y con vecino conectado. Las
                // tapiadas o locked no ofrecen casilla de cruce.
                if (door.CurrentState != DoorVisualState.Open) continue;
                if (!room.Connections.ContainsKey(door.Direction)) continue;

                // La puerta se spawnea en el anchor (sobre el borde); un paso hacia
                // adentro cae en la primera celda interior — misma convención que
                // PlayerRoomTransitioner.ResolveSpawnCoord.
                var doorCoord = grid.WorldToGrid(door.transform.position);
                map[doorCoord + door.Direction.InwardOffset()] = door.Direction;
            }

            return map;
        }
    }
}
