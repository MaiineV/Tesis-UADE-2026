using System;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Slot de puerta en un prefab de sala. TECHNICAL.md §13.3.
    /// <para>
    /// Cada <see cref="RoomLayout"/> expone 4 slots (N/S/E/W). Cuando el
    /// <c>DungeonManager</c> arma la conectividad: con vecino activa el
    /// <see cref="DoorRoot"/> y cablea el <c>DoorController</c> (visual Open =
    /// abierta, Locked = reja); sin vecino apaga el <see cref="DoorRoot"/>
    /// entero — sin camino no se ve puerta (CNF-012 v2).
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class DoorSlotRef
    {
        public DoorDirection Direction;

        [Tooltip("Transform world donde se instancia el DoorPrefab (pose + rotación).")]
        public Transform Anchor;

        [Tooltip("La reja (hijo del DoorRoot en los prefabs actuales). Visual de puerta " +
                 "bloqueada — la administra DoorController.SetState, el slot no la togglea.")]
        public GameObject WallPlug;

        [Tooltip("Opcional: mesh/collider autorado de la puerta abierta si no se usa un DoorPrefab instanciado.")]
        public GameObject DoorRoot;
    }

    public enum DoorDirection
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3
    }
}