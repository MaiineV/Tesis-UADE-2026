using System;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Slot de puerta en un prefab de sala. TECHNICAL.md §13.3.
    /// <para>
    /// Cada <see cref="RoomLayout"/> expone 4 slots (N/S/E/W). Cuando el
    /// <c>DungeonManager</c> arma la conectividad, si el slot tiene vecino
    /// instancia <see cref="DoorPrefab"/> sobre <see cref="Anchor"/> y desactiva
    /// <see cref="WallPlug"/>; si no, activa <see cref="WallPlug"/> (tapiado)
    /// y no instancia puerta.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class DoorSlotRef
    {
        public DoorDirection Direction;

        [Tooltip("Transform world donde se instancia el DoorPrefab (pose + rotación).")]
        public Transform Anchor;

        [Tooltip("Pared tapiada. Se activa cuando el slot no conecta con un vecino.")]
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