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
    }
}
