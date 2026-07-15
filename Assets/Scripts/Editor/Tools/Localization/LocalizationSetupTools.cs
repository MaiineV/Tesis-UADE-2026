using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;

namespace Rollgeon.EditorTools.Localization
{
    /// <summary>
    /// Utilidades de editor para el setup de localización (Feature ES/EN). Encapsulan
    /// las dos operaciones repetitivas: poblar String Table Collections y cablear un
    /// <see cref="LocalizeStringEvent"/> a un TMP. Pensadas para invocarse desde el MCP
    /// (<c>execute_code</c>) o desde otras herramientas de editor — no forman parte del
    /// runtime del juego.
    /// </summary>
    public static class LocalizationSetupTools
    {
        public const string EsCode = "es";
        public const string EnCode = "en";

        /// <summary>
        /// Crea (o actualiza) una key en la colección indicada con sus valores ES/EN.
        /// Idempotente: reejecutar sobrescribe los valores sin duplicar la key.
        /// No llama SaveAssets — el caller guarda tras el batch.
        /// </summary>
        public static void UpsertEntry(string collectionName, string key, string es, string en)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (collection == null)
                throw new Exception($"[LocalizationSetupTools] Collection '{collectionName}' no existe.");

            var shared = collection.SharedData;
            if (shared.GetEntry(key) == null)
                shared.AddKey(key);
            EditorUtility.SetDirty(shared);

            SetValue(collection, EsCode, key, es);
            SetValue(collection, EnCode, key, en);
        }

        private static void SetValue(StringTableCollection collection, string localeCode, string key, string value)
        {
            if (value == null) return;

            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null)
                throw new Exception($"[LocalizationSetupTools] La colección '{collection.TableCollectionName}' no tiene tabla para '{localeCode}'.");

            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            EditorUtility.SetDirty(table);
        }

        /// <summary>
        /// Agrega (o reusa) un <see cref="LocalizeStringEvent"/> en el GameObject del TMP
        /// y lo cablea a <c>collection/key</c>, con un persistent listener
        /// <c>OnUpdateString → TMP_Text.text</c> (sobrevive Play y build). Idempotente:
        /// reejecutar re-apunta la referencia y no duplica listeners.
        /// No marca la escena/prefab dirty ni guarda — el caller lo hace tras el batch.
        /// </summary>
        public static LocalizeStringEvent BindTMP(TMP_Text label, string collectionName, string key)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            var evt = label.GetComponent<LocalizeStringEvent>();
            if (evt == null)
                evt = label.gameObject.AddComponent<LocalizeStringEvent>();

            evt.StringReference.SetReference(collectionName, key);

            // Limpiar persistent listeners previos para no duplicar al reejecutar.
            for (int i = evt.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt.OnUpdateString, i);

            // Target el setter de la propiedad 'text' (mismo binding que el menú "Localize").
            var call = (UnityAction<string>)Delegate.CreateDelegate(
                typeof(UnityAction<string>), label, "set_text");
            UnityEventTools.AddPersistentListener(evt.OnUpdateString, call);

            EditorUtility.SetDirty(evt);
            return evt;
        }
    }
}
