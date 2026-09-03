using UnityEngine;

namespace Rollgeon.UI.Utility
{
    /// <summary>
    /// Punto de entrada de los call-sites al <see cref="IconPlaceholderMapSO"/>.
    /// Resuelve el asset lazy desde Resources para que las vistas no tengan que
    /// serializar una referencia al mapa cada una.
    /// </summary>
    /// <remarks>
    /// Sin el asset en Resources (EditMode tests, o el mapa todavía sin crear) devuelve el
    /// texto crudo con los <c>{TOKEN}</c> intactos en vez de tirar: la UI queda fea pero el
    /// juego no se rompe, y los tests pueden assertear contra el string sin markup.
    /// </remarks>
    public static class IconSpriteTags
    {
        public const string ResourcePath = "IconPlaceholderMap";

        private static IconPlaceholderMapSO _map;

        public static IconPlaceholderMapSO Map
        {
            get
            {
                if (_map == null)
                    _map = Resources.Load<IconPlaceholderMapSO>(ResourcePath);
                return _map;
            }
        }

        public static string ReplacePlaceholders(string text)
            => Map != null ? Map.ReplacePlaceholders(text) : text;

        public static string SpriteTag(string iconName)
            => $"<sprite name=\"{iconName}\">";

        /// <summary>
        /// Glifo del indicador de daño. Vive en el atlas <c>TMP_DmgIndicator</c>, fallback
        /// del default (installer <c>Rollgeon → UI → Wire Damage Indicator TMP Sprite</c>).
        /// </summary>
        public const string DamageIconName = "DmgIndicator";

        /// <summary>
        /// Un monto de daño con el ícono a su derecha (separados por un espacio) — TODO
        /// texto de tooltip que muestre daño pasa por acá, así el indicador es uno solo
        /// y escala con la fuente.
        /// </summary>
        public static string DamageAmount(int amount)
            => amount + " " + SpriteTag(DamageIconName);

        /// <summary>El ícono suelto, para cerrar una fórmula de daño no numérica.</summary>
        public static string DamageTag()
            => SpriteTag(DamageIconName);
    }
}
