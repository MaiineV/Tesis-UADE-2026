using System.Collections.Generic;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Todo lo que puede llevar un tooltip. Un blob de texto es el caso degenerado: sólo
    /// <see cref="Text"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Text"/> y <see cref="Name"/> conviven a propósito. El primero es el párrafo que
    /// pasan los siete tooltips que ya existen — puerta, casilla, acción, cofre — y se dibuja tal
    /// cual. El segundo es la identidad de una unidad, que encabeza su propia banda con los
    /// números al lado. Un solo campo obligaría a adivinar cuál de las dos cosas es.
    /// </remarks>
    public readonly struct TooltipContent
    {
        /// <summary>Párrafo suelto. Es lo único que trae un tooltip de texto.</summary>
        public readonly string Text;

        /// <summary>Nombre de la unidad, encabezando la banda de identidad.</summary>
        public readonly string Name;

        /// <summary>
        /// Familia de la unidad — <c>Jefe · Rango</c>. Va pegada al nombre y no al título: las dos
        /// son identidad, y el título es el hijo más ancho del panel, así que meterla ahí es lo
        /// que hace que el panel se ensanche. Vacía = no se dibuja la fila.
        /// </summary>
        public readonly string Type;

        public readonly int? Health;
        public readonly int? MaxHealth;
        public readonly int? Shield;

        public readonly IReadOnlyList<StatusIconState> Cards;

        /// <summary>Color de la unidad, al pie y en chico. Nunca arriba: no es información.</summary>
        public readonly string Flavor;

        // type va al final y con default para que nada de lo que ya construye un TooltipContent
        // —los siete tooltips de texto incluidos— tenga que tocarse.
        public TooltipContent(string text = null, string name = null,
                              IReadOnlyList<StatusIconState> cards = null, string flavor = null,
                              int? health = null, int? maxHealth = null, int? shield = null,
                              string type = null)
        {
            Text = text;
            Name = name;
            Cards = cards;
            Flavor = flavor;
            Health = health;
            MaxHealth = maxHealth;
            Shield = shield;
            Type = type;
        }

        public static TooltipContent FromText(string text, IReadOnlyList<StatusIconState> cards = null)
            => new TooltipContent(text: text, cards: cards);

        /// <summary>La fila de vitales necesita los dos números: "250" solo no dice nada.</summary>
        public bool HasVitals => Health.HasValue && MaxHealth.HasValue;

        public int CardCount => Cards?.Count ?? 0;

        /// <summary>Sin nada que decir el panel no se abre — un recuadro vacío es peor que nada.</summary>
        public bool IsEmpty
            => CardCount == 0 && !HasVitals
               && string.IsNullOrEmpty(Text)
               && string.IsNullOrEmpty(Name)
               && string.IsNullOrEmpty(Flavor);
    }
}
