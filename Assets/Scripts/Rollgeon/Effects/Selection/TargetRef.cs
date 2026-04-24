using System;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Referencia opaca a un target resuelto por una <see cref="BaseTargetQuery"/> o por
    /// el <c>SelectionController</c> runtime. TECHNICAL.md §11.1.
    /// <para>Tres formas excluyentes: por slot id (legacy), por entity guid, o por
    /// <see cref="Cell"/> de la grilla (usado por effects de movimiento / teleport a
    /// celda vacía).</para>
    /// </summary>
    public class TargetRef
    {
        public const int NoSlot = -1;

        /// <summary>Slot id legacy (<c>-1</c> cuando el ref no es por slot).</summary>
        public int SlotId = NoSlot;

        /// <summary>Guid de la entidad (<see cref="System.Guid.Empty"/> cuando el ref no es por entidad).</summary>
        public Guid Guid;

        /// <summary>
        /// Coordenada de grilla seleccionada. <c>HasCell == false</c> cuando el ref es por slot/entity.
        /// Consumido por <c>EffMove</c>, <c>EffTeleport</c> y cualquier effect que mueva a una celda.
        /// </summary>
        public GridCoord Cell;

        /// <summary><c>true</c> si <see cref="Cell"/> fue seteado por el constructor factory.</summary>
        public bool HasCell;

        /// <summary>Factory — referencia a un slot legacy (pre-GridCoord).</summary>
        public static TargetRef Slot(int slotId) => new TargetRef { SlotId = slotId, Guid = System.Guid.Empty };

        /// <summary>Factory — referencia a una entidad por su guid.</summary>
        public static TargetRef Entity(Guid guid) => new TargetRef { SlotId = NoSlot, Guid = guid };

        /// <summary>Factory — referencia a una celda de la grilla (movimiento, teleport).</summary>
        public static TargetRef FromCell(GridCoord cell) => new TargetRef
        {
            SlotId = NoSlot,
            Guid = System.Guid.Empty,
            Cell = cell,
            HasCell = true,
        };
    }
}
