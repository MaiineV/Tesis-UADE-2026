using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Read-only contract para navegación de piso — grafo de <see cref="RoomInstance"/>s
    /// en topología Isaac (TECHNICAL.md §13.6). Registrado en
    /// <see cref="Patterns.ServiceScope.Run"/> por <see cref="DungeonManager.CreateAndRegister"/>.
    /// <para>
    /// API breaking change 2026-04-22 — la vieja secuencia lineal (
    /// <c>NextRoom</c>/<c>CurrentRoomIndex</c>/<c>IsLastRoom</c>/<c>RoomCount</c>/<c>GetFloorRooms</c>)
    /// fue reemplazada por <see cref="EnterRoomByDoor"/> y el dict
    /// <see cref="GetAllRoomInstances"/> con <see cref="GetFloorShells"/>.
    /// </para>
    /// </summary>
    public interface IDungeonService
    {
        /// <summary>
        /// Template de la sala activa. Conveniencia = <see cref="CurrentRoomInstance"/>?.<see cref="RoomInstance.Template"/>.
        /// </summary>
        RoomSO CurrentRoom { get; }

        /// <summary>Nodo del grafo de la sala activa, o <c>null</c> pre-<see cref="GenerateFloor"/>.</summary>
        RoomInstance CurrentRoomInstance { get; }

        void GenerateFloor(FloorLayoutSO layout, int seed);

        /// <summary>Grafo completo del piso — key = <see cref="RoomInstance.InstanceId"/>.</summary>
        IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances();

        /// <summary>
        /// Metadata de los shells procedurales del piso — cubos transparentes
        /// en las cells no-activas consumidos por el floor view (§17.E.9).
        /// Key = <see cref="RoomInstance.InstanceId"/>. Los GameObjects reales
        /// los materializa el <c>FloorShellVisibilityController</c>.
        /// </summary>
        IReadOnlyDictionary<Guid, FloorShell> GetFloorShells();

        /// <summary>
        /// ¿Puede el player cruzar la puerta <paramref name="direction"/>?
        /// Requiere: vecino conectado + (sala <see cref="RoomState.Cleared"/> OR
        /// <see cref="State.DoorState.Forced"/>). Isaac-lock durante combate.
        /// </summary>
        bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId);

        /// <summary>
        /// Mueve al player a la sala conectada via <paramref name="direction"/>.
        /// Captura estado de la actual (HP enemigos + flags puertas) y restaura
        /// el de la destino si ya fue visitada.
        /// </summary>
        /// <returns><c>true</c> si la transición ocurrió.</returns>
        bool EnterRoomByDoor(DoorDirection direction);

        /// <summary>
        /// Debug / minimap hook — transición directa a una instancia del grafo
        /// sin validar conectividad o locks.
        /// </summary>
        bool EnterRoomByInstanceId(Guid instanceId);

        /// <summary>
        /// Bounds combinados del piso actual — unión de los shells procedurales.
        /// Consumido por <see cref="CameraService"/> para clampear pan (§17.E.6)
        /// y dimensionar la floor view (§17.E.9). <c>size == Vector3.zero</c>
        /// solo si el piso no fue generado todavía.
        /// </summary>
        Bounds GetFloorBounds();

        /// <summary>
        /// <see cref="WallOccluder"/>s de la sala activa. El <c>CameraService</c>
        /// los cruza con <see cref="CameraConfigSO.OcclusionMap"/> (§17.E.8).
        /// </summary>
        IReadOnlyList<WallOccluder> GetCurrentRoomOccluders();
    }

    /// <summary>
    /// Metadata de un shell procedural — cubo transparente que representa
    /// una sala del piso cuando la cámara está en floor view.
    /// </summary>
    public struct FloorShell
    {
        public Guid InstanceId;
        public Vector3 WorldPosition;
        public Vector3 Size;
    }
}
