using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rollgeon.Upgrades.Dice;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enchantment.Tests
{
    /// <summary>
    /// Auditoría permanente de cobertura: cierra los cuatro huecos que dejaron a los
    /// dos Codicioso huérfanos durante meses — assets fuera del catálogo (los saves los
    /// descartan), fuera del pool (no se ofrecen nunca), ids duplicados (el primero
    /// pisa al segundo al restaurar) y sin localización (la UI cae al texto del asset).
    /// Complementa a <c>EnchantmentAssetAuditTests</c>, que audita el contenido de cada
    /// asset; esto audita que cada asset esté conectado al juego.
    /// </summary>
    [TestFixture]
    public class EnchantmentCoverageAuditTests
    {
        static IEnumerable<(string path, EnchantmentSO ench)> AllEnchantmentAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench != null) yield return (path, ench);
            }
        }

        [Test]
        public void test_enchantments_every_asset_is_registered_in_the_catalog()
        {
            // Arrange
            var guids = AssetDatabase.FindAssets("t:" + nameof(EnchantmentCatalogSO));
            Assert.IsNotEmpty(guids, "EnchantmentCatalogSO no encontrado en el proyecto.");
            var catalog = AssetDatabase.LoadAssetAtPath<EnchantmentCatalogSO>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            var registered = new HashSet<EnchantmentSO>(catalog.Entries.Where(e => e != null));

            // Act
            var offenders = new StringBuilder();
            foreach (var (path, ench) in AllEnchantmentAssets())
            {
                if (!registered.Contains(ench))
                    offenders.AppendLine($"{path} (id '{ench.UpgradeId}')");
            }

            // Assert
            Assert.IsEmpty(offenders.ToString(),
                "Assets fuera del EnchantmentCatalog — los saves que los tengan los descartan al restaurar. "
                + "Registrarlos con EnchantmentAuthoring o el botón Add to Catalog:\n" + offenders);
        }

        [Test]
        public void test_enchantments_every_asset_is_in_the_altar_pool()
        {
            // Arrange
            var pool = EnchantmentPoolBridge.LoadDefaultPool();
            Assert.IsNotNull(pool, "EnchantmentPoolSO no encontrado en el proyecto.");

            // Act
            var offenders = new StringBuilder();
            foreach (var (path, ench) in AllEnchantmentAssets())
            {
                if (!EnchantmentPoolBridge.IsInPool(pool, ench))
                    offenders.AppendLine($"{path} (id '{ench.UpgradeId}')");
            }

            // Assert — para deshabilitar uno sin sacarlo, la entry va con Weight 0.
            Assert.IsEmpty(offenders.ToString(),
                "Assets fuera del pool del altar — no se ofrecen nunca:\n" + offenders);
        }

        [Test]
        public void test_enchantments_upgrade_ids_are_unique()
        {
            // Arrange + Act
            var byId = new Dictionary<string, List<string>>();
            foreach (var (path, ench) in AllEnchantmentAssets())
            {
                var id = ench.UpgradeId ?? string.Empty;
                if (!byId.TryGetValue(id, out var paths)) byId[id] = paths = new List<string>();
                paths.Add(path);
            }

            var offenders = new StringBuilder();
            foreach (var kv in byId)
            {
                if (string.IsNullOrEmpty(kv.Key))
                    offenders.AppendLine($"(sin id): {string.Join(", ", kv.Value)}");
                else if (kv.Value.Count > 1)
                    offenders.AppendLine($"'{kv.Key}': {string.Join(", ", kv.Value)}");
            }

            // Assert
            Assert.IsEmpty(offenders.ToString(),
                "Ids vacíos o duplicados — al restaurar un save, el primero encontrado pisa al resto:\n" + offenders);
        }

        [Test]
        public void test_enchantments_every_asset_has_localized_name_and_desc_in_both_locales()
        {
            // Arrange + Act — nombre Y descripción presentes en es y en. Que es ≠ en lo
            // audita test_localization_no_key_repeats_the_spanish_text_in_english.
            var offenders = new StringBuilder();
            foreach (var (path, ench) in AllEnchantmentAssets())
            {
                foreach (var locale in new[] { "es", "en" })
                {
                    var entry = EnchantmentLocalizationBridge.Read(ench.UpgradeId, locale);
                    if (string.IsNullOrWhiteSpace(entry.Name))
                        offenders.AppendLine($"{ench.UpgradeId}.name [{locale}] ({path})");
                    if (string.IsNullOrWhiteSpace(entry.Description))
                        offenders.AppendLine($"{ench.UpgradeId}.desc [{locale}] ({path})");
                }
            }

            // Assert
            Assert.IsEmpty(offenders.ToString(),
                "Claves de localización faltantes o vacías en la tabla Content — la UI cae al "
                + "texto del asset y el jugador en inglés ve español:\n" + offenders);
        }
    }
}
