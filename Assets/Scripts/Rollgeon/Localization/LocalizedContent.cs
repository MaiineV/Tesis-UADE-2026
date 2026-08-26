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
        public const string HintSuffix = ".hint";

        /// <summary>Nombre localizado del contenido, o <paramref name="fallback"/>.</summary>
        public static string Name(string entityId, string fallback)
            => Resolve(entityId, NameSuffix, fallback);

        /// <summary>Descripción localizada del contenido, o <paramref name="fallback"/>.</summary>
        public static string Description(string entityId, string fallback)
            => Resolve(entityId, DescSuffix, fallback);

        /// <summary>
        /// Como <see cref="Description"/> pero con argumentos, para las descripciones que llevan
        /// números vivos — el fuego que estás pisando cobra 6/10 o 15/15 según cuál sea.
        /// </summary>
        public static string DescriptionFormat(string entityId, string fallbackFormat,
                                               params object[] args)
            => string.IsNullOrEmpty(entityId)
                ? SafeFormat(fallbackFormat, args)
                : FromTableFormat(ContentTable, entityId + DescSuffix, fallbackFormat, args);

        /// <summary>
        /// <c>table[key]</c> formateado con <paramref name="args"/>, o el fallback formateado.
        /// </summary>
        /// <remarks>
        /// Va por <c>GetLocalizedString(args)</c> y no por un <c>string.Format</c> del resultado:
        /// hoy las entries no son Smart y las dos rutas coinciden, pero si alguna se marca Smart
        /// el formateo tiene que seguir siendo el del package y no el nuestro.
        /// </remarks>
        public static string FromTableFormat(string table, string key, string fallbackFormat,
                                             params object[] args)
        {
            if (!string.IsNullOrEmpty(key))
            {
                try
                {
                    var stringTable = LocalizationSettings.StringDatabase.GetTable(table);
                    var entry = stringTable?.GetEntry(key);
                    if (entry != null)
                    {
                        var localized = entry.GetLocalizedString(args);
                        if (!string.IsNullOrEmpty(localized)) return localized;
                    }
                }
                catch (Exception)
                {
                    // Localization no inicializado / tabla ausente → caemos al valor autor.
                }
            }

            return SafeFormat(fallbackFormat, args);
        }

        // El fallback tambien es un format string autorado a mano: un {0} de mas ahi no puede
        // tirar una excepcion adentro de un tooltip.
        private static string SafeFormat(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format)) return format;
            if (args == null || args.Length == 0) return format;

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// Pista localizada del contenido, o <paramref name="fallback"/>. La usan los
        /// desbloqueables, que muestran una pista mientras están bloqueados y recién
        /// revelan la <see cref="Description"/> al cumplirse.
        /// </summary>
        public static string Hint(string entityId, string fallback)
            => Resolve(entityId, HintSuffix, fallback);

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
