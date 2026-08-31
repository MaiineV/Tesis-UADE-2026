using System.Collections.Generic;
using Rollgeon.Items;
using Rollgeon.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Los textos localizados de un ítem, por idioma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Junta todas las llamadas a Unity.Localization en un lugar, igual que
    /// <see cref="ItemShopPriceBridge"/> hace con el <c>ShopPool</c>: el resto de la tool habla de
    /// "el nombre en español", no de colecciones, <c>SharedTableData</c> y <c>LocaleIdentifier</c>.
    /// </para>
    /// <para>
    /// <b>Por qué existe.</b> <c>ItemSO.DisplayName</c> no es lo que ve el jugador — es el fallback.
    /// <c>LocalizedContent.Name(itemId, so.DisplayName)</c> devuelve la entrada de la tabla
    /// <c>Content</c> si existe, y como el asistente siembra las dos keys al crear, el campo del
    /// asset queda pisado desde el minuto cero. Editarlo en la tool no cambiaba nada en el juego y
    /// nada lo avisaba: los textos reales sólo se podían tocar abriendo la ventana de Localization.
    /// </para>
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
        public static IReadOnlyList<string> Locales()
        {
            var result = new List<string>();
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                if (locale != null) result.Add(locale.Identifier.Code);

            // Alfabético y no el orden del proyecto: así la botonera no se reordena sola cuando
            // alguien agrega un idioma.
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        public static string DisplayNameOf(string localeCode)
        {
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                if (locale != null && locale.Identifier.Code == localeCode)
                    return locale.LocaleName;
            return localeCode;
        }

        /// <summary>Lo que hay hoy en la tabla para <paramref name="itemId"/> en ese idioma.</summary>
        public static Entry Read(string itemId, string localeCode)
        {
            var table = TableFor(localeCode);
            if (table == null || string.IsNullOrEmpty(itemId)) return default;

            return new Entry(
                table.GetEntry(itemId + LocalizedContent.NameSuffix)?.Value,
                table.GetEntry(itemId + LocalizedContent.DescSuffix)?.Value);
        }

        /// <summary>
        /// Escribe el nombre y la descripción de un idioma, con Undo.
        /// </summary>
        /// <remarks>
        /// Crea la key en la <c>SharedTableData</c> si falta: un ítem cuya key nunca se sembró (o a
        /// la que le borraron la entrada) tiene que poder empezar a traducirse desde acá igual.
        /// </remarks>
        public static void Write(string itemId, string localeCode, string name, string description)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return;

            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null) return;

            Undo.RecordObjects(
                new UnityEngine.Object[] { table, collection.SharedData }, "Edit Item Text");

            SetEntry(collection, table, itemId + LocalizedContent.NameSuffix, name);
            SetEntry(collection, table, itemId + LocalizedContent.DescSuffix, description);

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }

        static void SetEntry(StringTableCollection collection, StringTable table, string key, string value)
        {
            if (collection.SharedData.GetEntry(key) == null) collection.SharedData.AddKey(key);

            var entry = table.GetEntry(key);
            if (entry == null) table.AddEntry(key, value ?? string.Empty);
            else entry.Value = value ?? string.Empty;
        }

        static StringTable TableFor(string localeCode)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            return collection?.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
        }

        /// <summary>
        /// El idioma en el que se autora el proyecto. Es el que sincroniza el fallback del asset.
        /// </summary>
        public const string AuthoringLocale = "es";

        /// <summary>Lo que el juego mostraría hoy: la tabla si tiene entrada, si no el campo del asset.</summary>
        public static string EffectiveName(ItemSO item, string localeCode) =>
            item == null ? string.Empty : Read(item.ItemId, localeCode).Name ?? item.DisplayName;
    }
}
