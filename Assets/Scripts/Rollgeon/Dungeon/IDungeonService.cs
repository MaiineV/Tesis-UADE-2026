using System.Collections.Generic;
using Rollgeon.GameCamera;
using UnityEngine;

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

        /// <summary>
        /// Bounds combinados del piso actual — unión de los <c>LocalBounds</c>
        /// de cada <see cref="Components.RoomLayout"/> en su posición world.
        /// Consumido por el <c>CameraService</c> para clampear el pan
        /// (§17.E.6) y para dimensionar las shells del floor view (§17.E.9).
        /// </summary>
        /// <remarks>
        /// En el FP las rooms no se instancian como prefabs en world-space, por
        /// lo que el default es un bounds vacío (<c>size == Vector3.zero</c>).
        /// El consumidor debe treat ese caso como "sin clamp".
        /// </remarks>
        Bounds GetFloorBounds();

        /// <summary>
        /// <see cref="WallOccluder"/>s de la sala actual. El <c>CameraService</c>
        /// cruza sus <see cref="WallDirection"/>s contra
        /// <see cref="CameraConfigSO.OcclusionMap"/> para decidir qué paredes
        /// ocultar (§17.E.8). En el FP no hay prefabs instanciados, devuelve
        /// una lista vacía.
        /// </summary>
        IReadOnlyList<WallOccluder> GetCurrentRoomOccluders();
    }
}
