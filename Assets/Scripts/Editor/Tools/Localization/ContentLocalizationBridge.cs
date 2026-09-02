using System.Collections.Generic;
using Rollgeon.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.EditorTools.Localization
{
    /// <summary>
    /// Lectura/escritura de los pares <c>&lt;entityId&gt;.name</c> / <c>.desc</c> de la tabla
    /// <c>Content</c>, por idioma. Extraído de <c>ItemLocalizationBridge</c> para que
    /// items y encantamientos compartan la misma plomería en vez de duplicarla: la
    /// convención de claves es del proyecto entero, no de un dominio.
    /// </summary>
    public static class ContentLocalizationBridge
    {
        /// <summary>Los textos de una entidad en un idioma.</summary>
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

        /// <summary>El idioma en el que se autora el proyecto — sincroniza el fallback del asset.</summary>
        public const string AuthoringLocale = "es";

        /// <summary>Los códigos de locale del proyecto (<c>es</c>, <c>en</c>), en orden estable.</summary>
        public static IReadOnlyList<string> Locales()
        {
            var result = new List<string>();
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                if (locale != null) result.Add(locale.Identifier.Code);

            // Alfabético y no el orden del proyecto: así la botonera no se reordena sola
            // cuando alguien agrega un idioma.
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

        /// <summary>Lo que hay hoy en la tabla para <paramref name="entityId"/> en ese idioma.</summary>
        public static Entry Read(string entityId, string localeCode)
        {
            var table = TableFor(localeCode);
            if (table == null || string.IsNullOrEmpty(entityId)) return default;

            return new Entry(
                table.GetEntry(entityId + LocalizedContent.NameSuffix)?.Value,
                table.GetEntry(entityId + LocalizedContent.DescSuffix)?.Value);
        }

        /// <summary>
        /// Escribe el nombre y la descripción de un idioma, con Undo. Crea la key en la
        /// <c>SharedTableData</c> si falta.
        /// </summary>
        public static void Write(
            string entityId, string localeCode, string name, string description,
            string undoLabel = "Edit Localized Text")
        {
            if (string.IsNullOrEmpty(entityId)) return;

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return;

            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null) return;

            Undo.RecordObjects(
                new UnityEngine.Object[] { table, collection.SharedData }, undoLabel);

            SetEntry(collection, table, entityId + LocalizedContent.NameSuffix, name);
            SetEntry(collection, table, entityId + LocalizedContent.DescSuffix, description);

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }

        // ---- plomería de autoría (upsert/move/remove por clave) --------------------

        /// <summary>
        /// Upsert de una clave con textos es+en, envolviendo <c>LocalizationSetupTools.UpsertEntry</c>
        /// con los <c>Undo.RecordObject</c> que esa llamada omite (item-editor-spec §7 regla 3).
        /// </summary>
        public static void UpsertEntryWithUndo(string key, string es, string en, string undoLabel)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null)
                throw new System.Exception(
                    $"[ContentLocalizationBridge] String Table Collection '{LocalizedContent.ContentTable}' not found.");

            Undo.RecordObject(collection.SharedData, undoLabel);
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) is StringTable esTable)
                Undo.RecordObject(esTable, undoLabel);
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) is StringTable enTable)
                Undo.RecordObject(enTable, undoLabel);

            LocalizationSetupTools.UpsertEntry(LocalizedContent.ContentTable, key, es, en);
        }

        /// <summary>Mueve ambas claves (name+desc) de <paramref name="oldId"/> a <paramref name="newId"/>.</summary>
        public static void MoveEntityKeys(string oldId, string newId, string undoLabel)
        {
            MoveKey(oldId + LocalizedContent.NameSuffix, newId + LocalizedContent.NameSuffix, undoLabel);
            MoveKey(oldId + LocalizedContent.DescSuffix, newId + LocalizedContent.DescSuffix, undoLabel);
        }

        /// <summary>
        /// Lee los valores es/en de la clave vieja, los upsertea en la nueva y borra la
        /// vieja de todas las tablas. No-op si la clave vieja nunca tuvo texto.
        /// </summary>
        static void MoveKey(string oldKey, string newKey, string undoLabel)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return; // tabla ausente = problema de setup, no de esta llamada

            var esTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) as StringTable;
            var enTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) as StringTable;

            var esValue = esTable != null ? esTable.GetEntry(oldKey)?.Value : null;
            var enValue = enTable != null ? enTable.GetEntry(oldKey)?.Value : null;
            if (esValue == null && enValue == null) return;

            UpsertEntryWithUndo(newKey, esValue, enValue, undoLabel);

            Undo.RecordObject(collection.SharedData, undoLabel);
            RemoveKeyEverywhere(collection, oldKey, undoLabel);
            EditorUtility.SetDirty(collection.SharedData);
        }

        /// <summary>Borra <c>&lt;entityId&gt;.name</c> y <c>.desc</c> de <c>Content</c>. Devuelve cuántas sacó.</summary>
        public static int RemoveEntityKeys(string entityId, string undoLabel)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return 0;

            int removed = 0;
            Undo.RecordObject(collection.SharedData, undoLabel);
            foreach (var suffix in new[] { LocalizedContent.NameSuffix, LocalizedContent.DescSuffix })
            {
                if (RemoveKeyEverywhere(collection, entityId + suffix, undoLabel)) removed++;
            }

            if (removed == 0) return 0;

            EditorUtility.SetDirty(collection.SharedData);
            return removed;
        }

        /// <summary>
        /// Borra la clave de la shared data <b>y</b> de cada tabla de idioma — en ese orden
        /// inverso: primero las entradas por locale (indexadas por el id numérico de la
        /// definición), después la definición. <c>SharedTableData.RemoveKey</c> a secas deja
        /// entradas huérfanas por idioma dentro del <c>.asset</c>.
        /// </summary>
        static bool RemoveKeyEverywhere(StringTableCollection collection, string key, string undoLabel)
        {
            var shared = collection.SharedData.GetEntry(key);
            if (shared == null) return false;

            var id = shared.Id;
            foreach (var table in collection.StringTables)
            {
                if (table == null) continue;
                Undo.RecordObject(table, undoLabel);
                table.RemoveEntry(id);
                EditorUtility.SetDirty(table);
            }

            collection.SharedData.RemoveKey(key);
            return true;
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
    }
}
