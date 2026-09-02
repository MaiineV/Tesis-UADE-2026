using System.Collections.Generic;
using System.Linq;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// El nombre y la descripción del encantamiento, en el idioma elegido, dentro de "Identity".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>EnchantmentSO.DisplayName</c> no es lo que ve el jugador: es el respaldo. El juego lee
    /// la tabla <c>Content</c> por <c>UpgradeId</c> si tiene entrada, y el asistente siembra las
    /// dos keys al crear — el campo del asset queda pisado desde el minuto cero, y editarlo no
    /// cambiaba nada en el juego sin que nada lo avisara.
    /// </para>
    /// <para>
    /// Por eso los campos <b>reemplazan</b> a los del asset en su lugar de siempre, en vez de
    /// vivir en una sección aparte: dos pares de nombre y descripción en el mismo panel es
    /// exactamente la ambigüedad que hay que sacar. El campo crudo sigue disponible en Raw Data.
    /// </para>
    /// </remarks>
    public sealed partial class EnchantmentEditorWindow
    {
        const string LocalePrefKey = "Rollgeon.EnchantmentEditor.Locale";

        // Los campos de identidad viven en UpgradeSO como fields protegidos, así que nameof no
        // llega desde acá: los nombres van como literales, y son los que Odin usa como nombre de
        // miembro en el property tree.
        const string DisplayNameMember = "_displayName";
        const string DescriptionMember = "_description";

        IReadOnlyList<string> _locales;
        string _locale;
        GUIStyle _localeHelpStyle;
        string[] _localeLabels;

        IReadOnlyList<string> Locales => _locales ??= EnchantmentLocalizationBridge.Locales();

        string ActiveLocale
        {
            get
            {
                if (!string.IsNullOrEmpty(_locale)) return _locale;

                // Preferencia de quien edita, no del asset: se guarda por editor y sobrevive el
                // cambio de selección y el reinicio.
                _locale = EditorPrefs.GetString(LocalePrefKey, EnchantmentLocalizationBridge.AuthoringLocale);
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
                typeof(EnchantmentSO), DisplayNameMember, owner => DrawLocalizedName(owner as EnchantmentSO));
            PolymorphicBlockDrawer.RegisterMemberDrawer(
                typeof(EnchantmentSO), DescriptionMember, owner => DrawLocalizedDescription(owner as EnchantmentSO));
        }

        partial void OnLocalizationDisable()
        {
            // El registro es estático y la lambda captura esta ventana: sin soltarlo, cerrarla
            // dejaría el panel de la próxima escribiendo contra una instancia muerta.
            PolymorphicBlockDrawer.RegisterMemberDrawer(typeof(EnchantmentSO), DisplayNameMember, null);
            PolymorphicBlockDrawer.RegisterMemberDrawer(typeof(EnchantmentSO), DescriptionMember, null);
        }

        partial void OnLocalizationAssetsRefreshed()
        {
            _locales = null;
            _localeLabels = null;
        }

        void DrawLocalizedName(EnchantmentSO asset)
        {
            if (asset == null) return;

            DrawLocaleDropdown();

            if (string.IsNullOrEmpty(asset.UpgradeId))
            {
                EditorGUILayout.HelpBox(
                    "El encantamiento no tiene id, así que no tiene dónde guardar sus textos.",
                    MessageType.Warning);
                EditorGUILayout.LabelField("Nombre", asset.DisplayName);
                return;
            }

            var locale = ActiveLocale;
            var current = EnchantmentLocalizationBridge.Read(asset.UpgradeId, locale).Name ?? asset.DisplayName;

            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.TextField("Nombre", current);
            if (EditorGUI.EndChangeCheck()) WriteLocalizedName(asset, locale, next);
        }

        void DrawLocalizedDescription(EnchantmentSO asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.UpgradeId)) return;

            var locale = ActiveLocale;
            var current = EnchantmentLocalizationBridge.Read(asset.UpgradeId, locale).Description
                          ?? asset.Description;

            EditorGUILayout.LabelField("Descripción");
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.TextArea(current, GUILayout.MinHeight(48f));
            if (EditorGUI.EndChangeCheck()) WriteLocalizedDescription(asset, locale, next);

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
                .Select(code => $"{EnchantmentLocalizationBridge.DisplayNameOf(code)} ({code.ToUpperInvariant()})")
                .ToArray();

            int current = 0;
            for (int i = 0; i < locales.Count; i++)
                if (locales[i] == ActiveLocale) current = i;

            int next = EditorGUILayout.Popup("Idioma", current, _localeLabels);
            if (next != current) ActiveLocale = locales[next];
        }

        void WriteLocalizedName(EnchantmentSO asset, string locale, string value)
        {
            var entry = EnchantmentLocalizationBridge.Read(asset.UpgradeId, locale);
            EnchantmentLocalizationBridge.Write(
                asset.UpgradeId, locale, value, entry.Description ?? asset.Description);
            SyncFallback(asset, locale, value, null);
        }

        void WriteLocalizedDescription(EnchantmentSO asset, string locale, string value)
        {
            var entry = EnchantmentLocalizationBridge.Read(asset.UpgradeId, locale);
            EnchantmentLocalizationBridge.Write(
                asset.UpgradeId, locale, entry.Name ?? asset.DisplayName, value);
            SyncFallback(asset, locale, null, value);
        }

        /// <summary>
        /// En el idioma de autoría, el campo del asset acompaña al de la tabla.
        /// </summary>
        /// <remarks>
        /// Es lo que saca la trampa: con el asset diciendo una cosa y la tabla otra, el juego
        /// muestra la de la tabla y el campo crudo queda mintiendo para siempre. El español es el
        /// idioma en el que se autora este proyecto, así que es el que manda ahí.
        /// </remarks>
        void SyncFallback(EnchantmentSO asset, string locale, string name, string description)
        {
            if (locale != EnchantmentLocalizationBridge.AuthoringLocale) return;

            Context.Mutate("Edit Enchantment Text", () =>
            {
                if (name != null) asset.EditorSetDisplayName(name);
                if (description != null) asset.EditorSetDescription(description);
            });
        }

        void DrawLocaleHelp(string locale)
        {
            _localeHelpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            EditorGUILayout.LabelField(
                locale == EnchantmentLocalizationBridge.AuthoringLocale
                    ? "Es lo que ve el jugador. Al ser el idioma de autoría, también actualiza el " +
                      "texto de respaldo del asset."
                    : "Es lo que ve el jugador en ese idioma. El texto de respaldo del asset no " +
                      "cambia: sólo se usa cuando falta la traducción.",
                _localeHelpStyle);
        }
    }
}
