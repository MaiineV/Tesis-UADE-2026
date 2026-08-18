using System;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Lee un número que el Croupier tiene en juego y lo devuelve como índice de slot de la bolsa
    /// (número 1 → índice 0), o <c>-1</c> si no hay ninguno. Con este reader en
    /// <c>AINode_RotateBlock.DirectedIndex</c>, el sector que cae y el dado que se confisca son el
    /// mismo dato.
    /// </summary>
    /// <remarks>
    /// Las dos fuentes no son intercambiables y el orden dentro del turno importa:
    /// <c>AINode_DetonateSungSectors</c> vacía el windup, así que leer <c>Sung</c> después de la
    /// detonación devuelve el número siguiente sin fallar; y <c>AINode_IgniteDetonatedSectors</c>
    /// consume <c>DetonatedSectors</c>, así que un reader en ese modo tiene que correr antes.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AIReadCroupierWheelNumber : AIIntReader
    {
        /// <summary>De cuál de las dos listas del paño sale el número.</summary>
        public enum NumberSource
        {
            /// <summary>El número cantado y todavía sin detonar (el sector marcado en el overlay).</summary>
            Sung,

            /// <summary>El número que detonó este turno. Se consume en la ignición.</summary>
            Detonated,
        }

        [Tooltip("Sung = el número que acaba de cantar (todavía marcado). Detonated = el que acaba " +
                 "de caer este turno. No son el mismo dato: la detonación vacía el windup.")]
        public NumberSource Source = NumberSource.Sung;

        [Tooltip("Cuál de los números leer (0 = el primero). En fase 2 el jefe canta dos.")]
        [MinValue(0)]
        public int Slot;

        public override int Read(AIContext context)
        {
            if (!ServiceLocator.TryGetService<ICroupierWheelService>(out var wheel) || wheel == null)
                return -1;

            var numbers = Source == NumberSource.Detonated ? wheel.DetonatedSectors : wheel.SungNumbers;
            if (numbers == null || Slot < 0 || Slot >= numbers.Count) return -1;

            return numbers[Slot] - 1;
        }
    }
}
