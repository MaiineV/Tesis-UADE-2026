using System.Collections.Generic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Los textos localizados de un ítem, por idioma. Wrapper fino sobre
    /// <see cref="ContentLocalizationBridge"/> — la plomería de Unity.Localization vive
    /// allá (compartida con encantamientos); acá queda solo lo tipado a <see cref="ItemSO"/>.
    /// </summary>
    /// <remarks>
    /// <b>Por qué existe.</b> <c>ItemSO.DisplayName</c> no es lo que ve el jugador — es el fallback.
    /// <c>LocalizedContent.Name(itemId, so.DisplayName)</c> devuelve la entrada de la tabla
    /// <c>Content</c> si existe, y como el asistente siembra las dos keys al crear, el campo del
    /// asset queda pisado desde el minuto cero. Editarlo en la tool no cambiaba nada en el juego y
    /// nada lo avisaba: los textos reales sólo se podían tocar abriendo la ventana de Localization.
    /// </remarks>
    public static class ItemLocalizationBridge
    {
        /// <summary>Los textos de un ítem en un idioma.</summary>
        public readonly struct Entry
        {
            /// <summary><c>null</c> si la key no existe en la tabla — el juego cae al texto del asset.</summary>
            public string Name { get; }

            /// <summary><c>null</c> si la key no existe en la tabla.</summary>
            public string Description { get; }

            public Entry(string name, string description)
            {
                Name = name;
                Description = description;
            }
        }

        /// <summary>Los códigos de locale del proyecto (<c>es</c>, <c>en</c>), en orden estable.</summary>
        public static IReadOnlyList<string> Locales() => ContentLocalizationBridge.Locales();

        public static string DisplayNameOf(string localeCode) => ContentLocalizationBridge.DisplayNameOf(localeCode);

        /// <summary>Lo que hay hoy en la tabla para <paramref name="itemId"/> en ese idioma.</summary>
        public static Entry Read(string itemId, string localeCode)
        {
            var entry = ContentLocalizationBridge.Read(itemId, localeCode);
            return new Entry(entry.Name, entry.Description);
        }

        /// <summary>
        /// Escribe el nombre y la descripción de un idioma, con Undo. Crea la key en la
        /// <c>SharedTableData</c> si falta.
        /// </summary>
        public static void Write(string itemId, string localeCode, string name, string description)
            => ContentLocalizationBridge.Write(itemId, localeCode, name, description, "Edit Item Text");

        /// <summary>
        /// El idioma en el que se autora el proyecto. Es el que sincroniza el fallback del asset.
        /// </summary>
        public const string AuthoringLocale = ContentLocalizationBridge.AuthoringLocale;

        /// <summary>Lo que el juego mostraría hoy: la tabla si tiene entrada, si no el campo del asset.</summary>
        public static string EffectiveName(ItemSO item, string localeCode) =>
            item == null ? string.Empty : Read(item.ItemId, localeCode).Name ?? item.DisplayName;
    }
}
