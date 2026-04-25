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
    }
}
