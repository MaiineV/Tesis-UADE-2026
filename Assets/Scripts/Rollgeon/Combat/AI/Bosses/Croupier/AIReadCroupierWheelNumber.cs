using System;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Devuelve el número como índice de slot de la bolsa (número 1 → índice 0), o <c>-1</c>. El
    /// orden importa: <c>AINode_DetonateSungSectors</c> vacía el windup y la ignición consume
    /// <c>DetonatedSectors</c>, así que un reader en modo <c>Detonated</c> va antes de la ignición.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AIReadCroupierWheelNumber : AIIntReader
    {
        public enum NumberSource
        {
            /// <summary>El cantado y todavía sin detonar (el sector marcado en el overlay).</summary>
            Sung,

            /// <summary>El que detonó este turno. Se consume en la ignición.</summary>
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
