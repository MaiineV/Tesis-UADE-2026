using System;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Lee un número que el Croupier tiene en juego y lo devuelve como índice de slot de la bolsa
    /// (número 1 → índice 0). Es lo que hace que el sector que cae y el dado que se confisca sean el
    /// mismo dato: <c>AINode_RotateBlock</c> con este reader en <c>DirectedIndex</c> confisca el dado
    /// del número del paño en vez de sortear uno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos momentos, un solo número.</b> El número del Croupier vive en dos lugares distintos
    /// según en qué parte del turno se lo mire, y no son intercambiables:
    /// <list type="bullet">
    ///   <item><description><see cref="NumberSource.Sung"/> — <c>SungNumbers</c>, el número que
    ///   <b>acaba de cantar</b> y todavía no detonó. Es el sector que el jugador ve marcado en el
    ///   overlay y tiene un turno para esquivar.</description></item>
    ///   <item><description><see cref="NumberSource.Detonated"/> — <c>DetonatedSectors</c>, el
    ///   número que <b>acaba de resolverse</b> este turno.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Por qué la distinción no es cosmética.</b> <c>AINode_DetonateSungSectors</c> llama a
    /// <c>ConsumeWindup()</c>, que vacía el windup: apenas detona, <c>SungNumbers</c> ya no tiene el
    /// número que cayó — tiene el siguiente, o nada. Leer <c>Sung</c> después de la detonación
    /// devuelve el número equivocado sin fallar, que es la peor forma de estar mal.
    /// </para>
    /// <para>
    /// <b>Ventana de <c>Detonated</c>.</b> <c>AINode_IgniteDetonatedSectors</c> consume la lista con
    /// <c>ClearDetonated()</c>, así que un reader en este modo tiene que correr <b>antes</b> que la
    /// ignición dentro del mismo turno del jefe. El árbol del Croupier lo respeta por posición; hay
    /// un test que lo fija.
    /// </para>
    /// <para>
    /// Devuelve <c>-1</c> si no hay número donde mira, que <c>AINode_RotateBlock</c> interpreta como
    /// "no confisques nada" — un turno sin número no debería bloquear un dado al azar a escondidas.
    /// El caso de un número mayor que la bolsa (el 6 con una build de 5 dados) lo resuelve el nodo
    /// dando la vuelta al índice; ver su <c>DirectedIndex</c>.
    /// </para>
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
