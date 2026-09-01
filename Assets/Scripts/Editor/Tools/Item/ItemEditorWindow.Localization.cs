using System.Collections.Generic;
using System.Linq;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// El nombre y la descripción del ítem, en el idioma elegido, dentro de "Identity".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ItemSO.DisplayName</c> no es lo que ve el jugador: es el respaldo.
    /// <c>LocalizedContent.Name(itemId, so.DisplayName)</c> devuelve la entrada de la tabla
    /// <c>Content</c> si existe, y el asistente siembra las dos keys al crear — así que el campo del
    /// asset queda pisado desde el minuto cero. Editarlo no cambiaba nada en el juego y nada lo
    /// avisaba.
    /// </para>
    /// <para>
    /// Por eso los campos <b>reemplazan</b> a los del asset en su lugar de siempre, en vez de vivir
    /// en una sección aparte: dos pares de campos de nombre y descripción en el mismo panel es
    /// exactamente la ambigüedad que hay que sacar. El campo crudo sigue disponible en Raw Data.
    /// </para>
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        const string LocalePrefKey = "Rollgeon.ItemEditor.Locale";

        IReadOnlyList<string> _locales;
        string _locale;
        GUIStyle _localeHelpStyle;
        string[] _localeLabels;

        IReadOnlyList<string> Locales => _locales ??= ItemLocalizationBridge.Locales();

        string ActiveLocale
        {
            get
            {
                if (!string.IsNullOrEmpty(_locale)) return _locale;

                // Preferencia de quien edita, no del ítem: se guarda por editor y sobrevive el
                // cambio de selección y el reinicio.
                _locale = EditorPrefs.GetString(LocalePrefKey, ItemLocalizationBridge.AuthoringLocale);
                if (Locales.Count > 0 && !Locales.Contains(_locale)) _locale = Locales[0];
                return _locale;
            }
            set
            {
                _locale = value;
                EditorPrefs.SetString(LocalePrefKey, value);
            }
        }

        partial void OnLocalizationEnable()
        {
            PolymorphicBlockDrawer.RegisterMemberDrawer(
                typeof(ItemSO), nameof(ItemSO.DisplayName), owner => DrawLocalizedName(owner as ItemSO));
            PolymorphicBlockDrawer.RegisterMemberDrawer(
                typeof(ItemSO), nameof(ItemSO.Description), owner => DrawLocalizedDescription(owner as ItemSO));
        }

        partial void OnLocalizationDisable()
        {
            // El registro es estático y la lambda captura esta ventana: sin soltarlo, cerrarla dejaría
            // el panel de la próxima escribiendo contra una instancia muerta.
            PolymorphicBlockDrawer.RegisterMemberDrawer(typeof(ItemSO), nameof(ItemSO.DisplayName), null);
            PolymorphicBlockDrawer.RegisterMemberDrawer(typeof(ItemSO), nameof(ItemSO.Description), null);
        }

        partial void OnLocalizationAssetsRefreshed()
        {
            _locales = null;
            _localeLabels = null;
        }

        void DrawLocalizedName(ItemSO asset)
        {
            if (asset == null) return;

            DrawLocaleDropdown();

            if (string.IsNullOrEmpty(asset.ItemId))
            {
                EditorGUILayout.HelpBox(
                    "El ítem no tiene id, así que no tiene dónde guardar sus textos.", MessageType.Warning);
                EditorGUILayout.LabelField("Nombre", asset.DisplayName);
                return;
            }

            var locale = ActiveLocale;
            var current = ItemLocalizationBridge.Read(asset.ItemId, locale).Name ?? asset.DisplayName;

            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.TextField("Nombre", current);
            if (EditorGUI.EndChangeCheck()) WriteName(asset, locale, next);
        }

        void DrawLocalizedDescription(ItemSO asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.ItemId)) return;

            var locale = ActiveLocale;
            var current = ItemLocalizationBridge.Read(asset.ItemId, locale).Description ?? asset.Description;

            EditorGUILayout.LabelField("Descripción");
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.TextArea(current, GUILayout.MinHeight(48f));
            if (EditorGUI.EndChangeCheck()) WriteDescription(asset, locale, next);

            DrawLocaleHelp(locale);
        }

        /// <summary>
        /// En qué idioma se están editando los textos.
        /// </summary>
        /// <remarks>
        /// Desplegable y no una botonera: hoy son dos idiomas, pero una fila de botones deja de
        /// entrar en cuanto se sume un tercero, y este panel ya es angosto.
        /// </remarks>
        void DrawLocaleDropdown()
        {
            var locales = Locales;
            if (locales.Count == 0)
            {
                EditorGUILayout.HelpBox("El proyecto no tiene idiomas configurados.", MessageType.Warning);
                return;
            }

            _localeLabels ??= locales
                .Select(code => $"{ItemLocalizationBridge.DisplayNameOf(code)} ({code.ToUpperInvariant()})")
                .ToArray();

            int current = 0;
            for (int i = 0; i < locales.Count; i++)
                if (locales[i] == ActiveLocale) current = i;

            int next = EditorGUILayout.Popup("Idioma", current, _localeLabels);
            if (next != current) ActiveLocale = locales[next];
        }

        void WriteName(ItemSO asset, string locale, string value)
        {
            var entry = ItemLocalizationBridge.Read(asset.ItemId, locale);
            ItemLocalizationBridge.Write(asset.ItemId, locale, value, entry.Description ?? asset.Description);
            SyncFallback(asset, locale, value, null);
        }

        void WriteDescription(ItemSO asset, string locale, string value)
        {
            var entry = ItemLocalizationBridge.Read(asset.ItemId, locale);
            ItemLocalizationBridge.Write(asset.ItemId, locale, entry.Name ?? asset.DisplayName, value);
            SyncFallback(asset, locale, null, value);
        }

        /// <summary>
        /// En el idioma de autoría, el campo del asset acompaña al de la tabla.
        /// </summary>
        /// <remarks>
        /// Es lo que saca la trampa: con el asset diciendo una cosa y la tabla otra, el juego muestra
        /// la de la tabla y el campo crudo queda mintiendo para siempre. El español es el idioma en
        /// el que se autora este proyecto, así que es el que manda ahí.
        /// </remarks>
        void SyncFallback(ItemSO asset, string locale, string name, string description)
        {
            if (locale != ItemLocalizationBridge.AuthoringLocale) return;

            Context.Mutate("Edit Item Text", () =>
            {
                if (name != null) asset.DisplayName = name;
                if (description != null) asset.Description = description;
            });
        }

        void DrawLocaleHelp(string locale)
        {
            _localeHelpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            EditorGUILayout.LabelField(
                locale == ItemLocalizationBridge.AuthoringLocale
                    ? "Es lo que ve el jugador. Al ser el idioma de autoría, también actualiza el " +
                      "texto de respaldo del asset."
                    : "Es lo que ve el jugador en ese idioma. El texto de respaldo del asset no " +
                      "cambia: sólo se usa cuando falta la traducción.",
                _localeHelpStyle);
        }
    }
}
