using System.Collections.Generic;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Read-only contract for dungeon floor navigation (TECHNICAL.md §11b).
    /// Registered in <see cref="Patterns.ServiceScope.Run"/> by
    /// <see cref="DungeonManager.CreateAndRegister"/>.
    /// </summary>
    public interface IDungeonService
    {
        RoomSO CurrentRoom { get; }
        int CurrentRoomIndex { get; }
        int RoomCount { get; }
        bool IsLastRoom { get; }
        void GenerateFloor(FloorLayoutSO layout, int seed);
        bool NextRoom();
        IReadOnlyList<RoomSO> GetFloorRooms();
    }
}
