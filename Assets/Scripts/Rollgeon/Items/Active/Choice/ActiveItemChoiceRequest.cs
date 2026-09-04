using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Items.Active.Choice
{
    /// <summary>
    /// Pedido de eleccion post-tirada de un efecto de banda. GDD §A5. El servicio abre
    /// una seleccion sintetica sobre <see cref="Options"/> y llama exactamente uno de
    /// los dos callbacks segun como termine.
    /// </summary>
    public sealed class ActiveItemChoiceRequest
    {
        /// <summary>Tiles entre los que el jugador elige. El efecto ya las filtro (seguras, distintas, etc).</summary>
        public IReadOnlyList<GridCoord> Options;

        /// <summary>Estilo de highlight de las opciones. Default "range".</summary>
        public string HighlightStyle = "range";

        /// <summary>El jugador eligio una opcion.</summary>
        public Action<GridCoord> OnChosen;

        /// <summary>
        /// La eleccion se abandono (cancelo, fin de turno o fin de combate). El roll ya
        /// se pago — el efecto debe resolver algo razonable (ej. una opcion al azar),
        /// nunca dejar el estado a medio aplicar.
        /// </summary>
        public Action OnAbandoned;
    }
}
