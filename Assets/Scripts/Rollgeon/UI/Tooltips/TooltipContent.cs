using System.Collections.Generic;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Todo lo que puede llevar un tooltip. <see cref="Text"/> (párrafo) y <see cref="Name"/>
    /// (identidad) conviven a propósito: un solo campo obligaría a adivinar cuál es.
    /// </summary>
    public readonly struct TooltipContent
    {
        /// <summary>Párrafo suelto. Es lo único que trae un tooltip de texto.</summary>
        public readonly string Text;

        /// <summary>Nombre de la unidad, encabezando la banda de identidad.</summary>
        public readonly string Name;

        /// <summary>Familia de la unidad — <c>Jefe · Rango</c>. Vacía = no se dibuja.</summary>
        public readonly string Type;

        public readonly int? Health;
        public readonly int? MaxHealth;
        public readonly int? Shield;

        /// <summary>Lo que va a hacer: la columna de arriba.</summary>
        public readonly IReadOnlyList<StatusIconState> Cards;

        /// <summary>Lo que le pasa: al costado, para no estirar el panel mientras se lee.</summary>
        public readonly IReadOnlyList<StatusIconState> SideCards;

        /// <summary>Debajo de la caja (la debilidad), por lo mismo que <see cref="SideCards"/>.</summary>
        public readonly IReadOnlyList<StatusIconState> BottomCards;

        /// <summary>Color de la unidad, al pie y en chico. Nunca arriba: no es información.</summary>
        public readonly string Flavor;

        // Parámetros nuevos al final y con default: los llamadores existentes no se tocan.
        public TooltipContent(string text = null, string name = null,
                              IReadOnlyList<StatusIconState> cards = null, string flavor = null,
                              int? health = null, int? maxHealth = null, int? shield = null,
                              string type = null,
                              IReadOnlyList<StatusIconState> sideCards = null,
                              IReadOnlyList<StatusIconState> bottomCards = null)
        {
            Text = text;
            Name = name;
            Cards = cards;
            Flavor = flavor;
            Health = health;
            MaxHealth = maxHealth;
            Shield = shield;
            Type = type;
            SideCards = sideCards;
            BottomCards = bottomCards;
        }

        public static TooltipContent FromText(string text, IReadOnlyList<StatusIconState> cards = null)
            => new TooltipContent(text: text, cards: cards);

        /// <summary>La fila de vitales necesita los dos números: "250" solo no dice nada.</summary>
        public bool HasVitals => Health.HasValue && MaxHealth.HasValue;

        public int CardCount => Cards?.Count ?? 0;

        public int SideCardCount => SideCards?.Count ?? 0;

        public int BottomCardCount => BottomCards?.Count ?? 0;

        /// <summary>Sin nada que decir el panel no se abre — un recuadro vacío es peor que nada.</summary>
        public bool IsEmpty
            => CardCount == 0 && SideCardCount == 0 && BottomCardCount == 0 && !HasVitals
               && string.IsNullOrEmpty(Text)
               && string.IsNullOrEmpty(Name)
               && string.IsNullOrEmpty(Flavor);
    }
}
