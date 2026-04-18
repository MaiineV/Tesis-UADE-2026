using System;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Referencia opaca a un target resuelto por una <see cref="BaseTargetQuery"/> o por
    /// el <c>SelectionController</c> runtime. TECHNICAL.md §11.1.
    /// <para>Dos formas: por slot id (cuando el target es una casilla de la grilla) o por
    /// entity guid (cuando el target es una entidad concreta). Son excluyentes en el uso
    /// — un <see cref="TargetRef"/> construido con <see cref="Slot"/> lleva
    /// <see cref="Guid"/> en <c>Guid.Empty</c> y viceversa.</para>
    /// </summary>
    public class TargetRef
    {
        /// <summary>Slot id del grid (<c>-1</c> cuando el ref es por entity).</summary>
        public int SlotId;

        /// <summary>Guid de la entidad (<see cref="System.Guid.Empty"/> cuando el ref es por slot).</summary>
        public Guid Guid;

        /// <summary>Factory — referencia a un slot concreto de la grilla.</summary>
        public static TargetRef Slot(int slotId) => new TargetRef { SlotId = slotId, Guid = System.Guid.Empty };

        /// <summary>Factory — referencia a una entidad por su guid.</summary>
        public static TargetRef Entity(Guid guid) => new TargetRef { SlotId = -1, Guid = guid };
    }
}
