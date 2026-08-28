using System.Collections.Generic;
using System.Linq;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// La sección "Textos": el nombre y la descripción que ve el jugador, por idioma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hasta acá el panel dibujaba <c>DisplayName</c> y <c>Description</c> del asset y nada más — y
    /// esos campos son el <b>fallback</b>, no lo que ve el jugador: gana la tabla <c>Content</c>, que
    /// el asistente siembra al crear. O sea que editar el nombre en la tool no cambiaba nada en el
    /// juego, sin ningún aviso, y para tocar el texto real había que abrir la ventana de
    /// Localization y buscar la key a mano.
    /// </para>
    /// <para>
    /// La idea no es reorganizar la tabla sino que el diseñador no tenga que verla.
    /// </para>
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        const string LocalePrefKey = "Rollgeon.ItemEditor.Locale";

        IReadOnlyList<string> _locales;
        string _locale;
        GUIStyle _localeHelpStyle;

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

        partial void OnLocalizationAssetsRefreshed()
        {
            _locales = null;
        }

        partial void DrawLocalizationExtras(ItemSO asset)
        {
            if (asset == null) return;
            if (!DrawExtrasSection("Textos")) return;

            if (string.IsNullOrEmpty(asset.ItemId))
            {
                EditorGUILayout.HelpBox(
                    "El ítem no tiene id, así que no tiene dónde guardar sus textos.", MessageType.Warning);
                return;
            }

            var locales = Locales;
            if (locales.Count == 0)
            {
                EditorGUILayout.HelpBox("El proyecto no tiene idiomas configurados.", MessageType.Warning);
                return;
            }

            DrawLocaleBar(locales);

            var locale = ActiveLocale;
            var entry = ItemLocalizationBridge.Read(asset.ItemId, locale);

            if (entry.Name == null && entry.Description == null)
                EditorGUILayout.HelpBox(
                    $"Sin texto en {ItemLocalizationBridge.DisplayNameOf(locale)}: el juego muestra el " +
                    "del asset. Escribí acá para traducirlo.", MessageType.Info);

            var name = entry.Name ?? asset.DisplayName;
            var description = entry.Description ?? asset.Description;

            EditorGUI.BeginChangeCheck();
            var nextName = EditorGUILayout.TextField("Nombre", name);
            EditorGUILayout.LabelField("Descripción");
            var nextDescription = EditorGUILayout.TextArea(description, GUILayout.MinHeight(48f));
            if (EditorGUI.EndChangeCheck())
                WriteTexts(asset, locale, nextName, nextDescription);

            DrawLocaleHelp(locale);
        }

        void DrawLocaleBar(IReadOnlyList<string> locales)
        {
            var labels = new string[locales.Count];
            int current = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                labels[i] = locales[i].ToUpperInvariant();
                if (locales[i] == ActiveLocale) current = i;
            }

            int next = GUILayout.Toolbar(current, labels);
            if (next != current) ActiveLocale = locales[next];
        }

        /// <summary>
        /// Guarda el texto y, en el idioma de autoría, también el fallback del asset.
        /// </summary>
        /// <remarks>
        /// Sincronizar el fallback es lo que saca la trampa: si el campo del asset dice una cosa y la
        /// tabla otra, el juego muestra el de la tabla y el panel de "Identity" queda mintiendo. El
        /// español es el idioma en el que se autora este proyecto, así que es el que manda ahí.
        /// </remarks>
        void WriteTexts(ItemSO asset, string locale, string name, string description)
        {
            ItemLocalizationBridge.Write(asset.ItemId, locale, name, description);

            if (locale != ItemLocalizationBridge.AuthoringLocale) return;

            Context.Mutate("Edit Item Text", () =>
            {
                asset.DisplayName = name;
                asset.Description = description;
            });
        }

        void DrawLocaleHelp(string locale)
        {
            _localeHelpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            EditorGUILayout.LabelField(
                locale == ItemLocalizationBridge.AuthoringLocale
                    ? "Esto es lo que ve el jugador. Al ser el idioma de autoría, también actualiza " +
                      "el nombre y la descripción del asset, que son el texto de respaldo."
                    : "Esto es lo que ve el jugador en ese idioma. El nombre del asset no cambia: " +
                      "es sólo el respaldo cuando falta la traducción.",
                _localeHelpStyle);
        }
    }
}
