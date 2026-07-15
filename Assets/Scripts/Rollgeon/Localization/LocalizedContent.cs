using System;
using UnityEngine.Localization.Settings;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Resolver central para el texto <b>data-driven</b> (nombres/descripciones de
    /// héroes, enemigos, combos, items, upgrades, salas, unlocks…). Busca en la String
    /// Table <c>Content</c> una entry keyeada por <c>&lt;entityId&gt;&lt;suffix&gt;</c>
    /// en el locale activo y, si no existe (o Localization aún no inicializó), devuelve
    /// el <paramref name="fallback"/> — típicamente el valor autor del ScriptableObject.
    /// <para>
    /// Así "localizar todo el contenido" se reduce a poblar la tabla <c>Content</c>: los
    /// call-sites solo envuelven su lectura (<c>so.DisplayName</c>) con
    /// <see cref="Name"/> / <see cref="Description"/> pasando el id y el valor autor como
    /// fallback, sin acoplarse al package.
    /// </para>
    /// </summary>
    public static class LocalizedContent
    {
        public const string ContentTable = "Content";
        public const string UITable = "UI";
        public const string NameSuffix = ".name";
        public const string DescSuffix = ".desc";

        /// <summary>Nombre localizado del contenido, o <paramref name="fallback"/>.</summary>
        public static string Name(string entityId, string fallback)
            => Resolve(entityId, NameSuffix, fallback);

        /// <summary>Descripción localizada del contenido, o <paramref name="fallback"/>.</summary>
        public static string Description(string entityId, string fallback)
            => Resolve(entityId, DescSuffix, fallback);

        /// <summary>
        /// Devuelve <c>Content[entityId + suffix]</c> en el locale activo, o
        /// <paramref name="fallback"/> si falta la key, la tabla no está cargada, o
        /// Localization todavía no inicializó.
        /// </summary>
        public static string Resolve(string entityId, string suffix, string fallback)
        {
            if (string.IsNullOrEmpty(entityId)) return fallback;
            return FromTable(ContentTable, entityId + suffix, fallback);
        }

        /// <summary>
        /// Texto de chrome desde la tabla <c>UI</c> por key exacta, para los pocos labels
        /// que se setean por código (ej. títulos de toasts). Los labels estáticos usan
        /// <c>LocalizeStringEvent</c> directamente y no pasan por acá.
        /// </summary>
        public static string Ui(string key, string fallback)
            => FromTable(UITable, key, fallback);

        /// <summary>
        /// Busca <c>table[key]</c> en el locale activo; devuelve <paramref name="fallback"/> si
        /// falta la key, la tabla no está cargada, o Localization todavía no inicializó.
        /// </summary>
        public static string FromTable(string table, string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            try
            {
                var stringTable = LocalizationSettings.StringDatabase.GetTable(table);
                if (stringTable != null)
                {
                    var entry = stringTable.GetEntry(key);
                    if (entry != null)
                    {
                        var localized = entry.GetLocalizedString();
                        if (!string.IsNullOrEmpty(localized)) return localized;
                    }
                }
            }
            catch (Exception)
            {
                // Localization no inicializado / tabla ausente → caemos al valor autor.
            }

            return fallback;
        }
    }
}
