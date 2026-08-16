using System;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Lee el número que el Croupier tiene en el aire y lo devuelve como índice de slot de la bolsa
    /// (número 1 → índice 0). Es lo que hace que el sector que cae y el dado que se confisca sean el
    /// mismo dato: <c>AINode_RotateBlock</c> con este reader en <c>DirectedIndex</c> confisca el dado
    /// del número cantado en vez de sortear uno.
    /// <para>
    /// <b>Hoy no lo usa nadie, y está huérfano a propósito.</b> Al Croupier se le sacó la
    /// confiscación porque robar un dado sin ninguna presentación es indistinguible del bloqueo
    /// aleatorio del Sunken Grand. Si vuelve —junto con su visual: el dado viajando a la mesa y el
    /// slot con candado— esta es la pieza que hace que el dado robado sea el del número cantado.
    /// No borrar en una limpieza de código muerto.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Devuelve <c>-1</c> si no hay número en el aire, que <c>AINode_RotateBlock</c> interpreta como
    /// "no confisques nada" — un turno sin número cantado no debería bloquear un dado al azar a
    /// escondidas. El caso de un número mayor que la bolsa (el 6 con una build de 5 dados) lo resuelve
    /// el nodo dando la vuelta al índice; ver su <c>DirectedIndex</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AIReadCroupierWheelNumber : AIIntReader
    {
        [Tooltip("Cuál de los números cantados leer (0 = el primero). En fase 2 el jefe canta dos.")]
        [MinValue(0)]
        public int Slot;

        public override int Read(AIContext context)
        {
            if (!ServiceLocator.TryGetService<ICroupierWheelService>(out var wheel) || wheel == null)
                return -1;

            var numbers = wheel.SungNumbers;
            if (numbers == null || Slot < 0 || Slot >= numbers.Count) return -1;

            return numbers[Slot] - 1;
        }
    }
}
