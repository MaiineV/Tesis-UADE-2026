using System.Collections.Generic;
using System.Linq;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item
{
    public static partial class ItemQuery
    {
        /// <summary>Los textos de un ítem en un idioma, tal como los devuelve la tabla.</summary>
        public delegate ItemLocalizationBridge.Entry LocalizedTextLookup(string itemId, string localeCode);

        /// <summary>
        /// Ítems sin traducir, para la lista de problemas.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Separado de <see cref="CheckCatalogHealth(IEnumerable{ItemSO}, Rollgeon.Shop.ShopPoolSO)"/>
        /// a propósito: ese es puro — sus tests arman ítems en memoria y no tocan un solo asset del
        /// proyecto — y leer las tablas de Localization desde ahí rompería esa garantía. Acá el
        /// lookup se inyecta, así los tests siguen sin depender del estado del proyecto.
        /// </para>
        /// <para>
        /// La regla de "español repetido en inglés" es la misma que
        /// <c>LocalizationTablesTests.test_localization_no_key_repeats_the_spanish_text_in_english</c>:
        /// hoy eso se descubre en el CI, con el nombre de una key, y acá se ve en la tool con el
        /// nombre del ítem.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<CatalogFinding> CheckLocalizationHealth(
            IEnumerable<ItemSO> items,
            IReadOnlyList<string> locales = null,
            LocalizedTextLookup lookup = null)
        {
            var findings = new List<CatalogFinding>();
            var list = (items ?? Enumerable.Empty<ItemSO>()).Where(i => i != null).ToList();
            if (list.Count == 0) return findings;

            locales ??= ItemLocalizationBridge.Locales();
            lookup ??= ItemLocalizationBridge.Read;
            if (locales.Count == 0) return findings;

            foreach (var item in list)
            {
                if (string.IsNullOrEmpty(item.ItemId)) continue;   // ya lo reporta CheckCatalogHealth
                var label = LabelOf(item);

                var byLocale = new Dictionary<string, ItemLocalizationBridge.Entry>();
                foreach (var locale in locales) byLocale[locale] = lookup(item.ItemId, locale);

                foreach (var locale in locales)
                {
                    var entry = byLocale[locale];
                    var up = locale.ToUpperInvariant();

                    if (entry.Name == null && entry.Description == null)
                    {
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' no tiene texto en {up} — el juego muestra el del asset, sin traducir.",
                            item));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.Name))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning, $"'{label}' no tiene nombre en {up}.", item));

                    if (string.IsNullOrWhiteSpace(entry.Description))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning, $"'{label}' no tiene descripción en {up}.", item));
                }

                // El mismo texto en dos idiomas es una columna copiada, no una traducción. Se compara
                // cada par una sola vez, no ida y vuelta.
                for (int i = 0; i < locales.Count; i++)
                for (int j = i + 1; j < locales.Count; j++)
                {
                    var a = locales[i];
                    var b = locales[j];
                    if (SameText(byLocale[a].Name, byLocale[b].Name))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' tiene el mismo nombre en {a.ToUpperInvariant()} y " +
                            $"{b.ToUpperInvariant()} — falta traducirlo.",
                            item));

                    if (SameText(byLocale[a].Description, byLocale[b].Description))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' tiene la misma descripción en {a.ToUpperInvariant()} y " +
                            $"{b.ToUpperInvariant()} — falta traducirla.",
                            item));
                }
            }

            return findings;
        }

        static bool SameText(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && a == b;
    }
}
