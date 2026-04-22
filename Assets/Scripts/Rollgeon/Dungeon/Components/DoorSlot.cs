using System;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Metadata de una puerta entre salas. TECHNICAL.md §13.3.
    /// </summary>
    [Serializable]
    public struct DoorSlot
    {
        public DoorDirection Direction;
        public GridCoord Coord;
        [Tooltip("Si la puerta empieza cerrada (se abrirá al clearear la sala).")]
        public bool StartsLocked;
    }

    public enum DoorDirection { North, South, East, West }
}
