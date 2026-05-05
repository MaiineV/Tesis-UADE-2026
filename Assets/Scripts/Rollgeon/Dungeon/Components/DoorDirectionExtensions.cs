using Rollgeon.Grid;

namespace Rollgeon.Dungeon.Components
{
    public static class DoorDirectionExtensions
    {
        public static DoorDirection Opposite(this DoorDirection dir) => dir switch
        {
            DoorDirection.North => DoorDirection.South,
            DoorDirection.South => DoorDirection.North,
            DoorDirection.East  => DoorDirection.West,
            DoorDirection.West  => DoorDirection.East,
            _                   => dir,
        };

        public static string DoorStateKey(this DoorDirection dir) => dir switch
        {
            DoorDirection.North => "door_N",
            DoorDirection.South => "door_S",
            DoorDirection.East  => "door_E",
            DoorDirection.West  => "door_W",
            _                   => "door_?",
        };

        /// <summary>
        /// Offset en grid coords desde la puerta hacia el interior de la sala (un paso hacia
        /// adentro). Convención del proyecto: GridCoord.X ↔ world X, GridCoord.Y ↔ world Z;
        /// North = +Z, así que la puerta Norte mira al +Y del grid y entrar requiere ir a -Y.
        /// </summary>
        public static GridCoord InwardOffset(this DoorDirection dir) => dir switch
        {
            DoorDirection.North => new GridCoord(0, -1),
            DoorDirection.South => new GridCoord(0, +1),
            DoorDirection.East  => new GridCoord(-1, 0),
            DoorDirection.West  => new GridCoord(+1, 0),
            _                   => GridCoord.Zero,
        };
    }
}
