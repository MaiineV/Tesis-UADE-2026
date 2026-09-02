using System.Collections.Generic;
using System.Linq;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Enchantment
{
    public static partial class EnchantmentQuery
    {
        /// <summary>
        /// Cómo leer los textos de un encantamiento en un idioma. Se inyecta para que los
        /// tests no toquen las tablas del proyecto — el default es
        /// <see cref="EnchantmentLocalizationBridge.Read"/>.
        /// </summary>
        public delegate EnchantmentLocalizationBridge.Entry LocalizedTextLookup(string upgradeId, string localeCode);

        /// <summary>
        /// Faltantes de traducción, espejo de <c>ItemQuery.CheckLocalizationHealth</c>:
        /// sin entrada en un idioma, textos vacíos, y mismo texto en dos idiomas (lo que
        /// el test de CI <c>test_localization_no_key_repeats_the_spanish_text_in_english</c>
        /// va a rechazar).
        /// </summary>
        public static IReadOnlyList<CatalogFinding> CheckLocalizationHealth(
            IEnumerable<EnchantmentSO> enchantments,
            IReadOnlyList<string> locales = null,
            LocalizedTextLookup lookup = null)
        {
            var findings = new List<CatalogFinding>();
            locales ??= EnchantmentLocalizationBridge.Locales();
            lookup ??= EnchantmentLocalizationBridge.Read;

            foreach (var ench in (enchantments ?? Enumerable.Empty<EnchantmentSO>()).Where(e => e != null))
            {
                if (string.IsNullOrEmpty(ench.UpgradeId)) continue; // eso ya lo reporta CheckCatalogHealth

                var label = LabelOf(ench);
                var byLocale = new Dictionary<string, EnchantmentLocalizationBridge.Entry>();

                foreach (var locale in locales)
                {
                    var entry = lookup(ench.UpgradeId, locale);
                    byLocale[locale] = entry;

                    if (entry.Name == null && entry.Description == null)
                    {
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' no tiene textos en '{locale}' — el juego cae al texto del asset.",
                            ench));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.Name))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning, $"'{label}' tiene el nombre vacío en '{locale}'.", ench));
                    if (string.IsNullOrWhiteSpace(entry.Description))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning, $"'{label}' tiene la descripción vacía en '{locale}'.", ench));
                }

                // De a pares, una sola vez por par: mismo texto en dos idiomas = sin traducir.
                for (int i = 0; i < locales.Count; i++)
                {
                    for (int j = i + 1; j < locales.Count; j++)
                    {
                        var a = byLocale[locales[i]];
                        var b = byLocale[locales[j]];

                        if (!string.IsNullOrWhiteSpace(a.Name) && a.Name == b.Name)
                            findings.Add(new CatalogFinding(
                                FindingSeverity.Warning,
                                $"'{label}' tiene el mismo nombre en '{locales[i]}' y '{locales[j]}' — falta traducir.",
                                ench));
                        if (!string.IsNullOrWhiteSpace(a.Description) && a.Description == b.Description)
                            findings.Add(new CatalogFinding(
                                FindingSeverity.Warning,
                                $"'{label}' tiene la misma descripción en '{locales[i]}' y '{locales[j]}' — falta traducir.",
                                ench));
                    }
                }
            }

            return findings;
        }
    }
}
