using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Meta;
using Rollgeon.Tutorial;
using Rollgeon.UI;
using Rollgeon.UI.Help;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.Editor.Tools.Localization.Tests
{
    /// <summary>
    /// Guardia de las String Table Collections. Los tres bugs de idioma que motivaron
    /// esto (tutorial sin keys, pistas de unlocks crudas, columna EN de los
    /// encantamientos copiada del español) eran todos <b>agujeros de data</b>, no de
    /// código: se detectan comparando las tablas contra sí mismas, no jugando.
    /// Poblar las tablas es responsabilidad de <c>LocalizationContentSeeder</c>.
    /// </summary>
    public class LocalizationTablesTests
    {
        private const string EsCode = "es";
        private const string EnCode = "en";

        /// <summary>
        /// Keys cuyo valor es legítimamente idéntico en los dos idiomas: nombres
        /// propios y términos de juego que no se traducen. Cualquier otra coincidencia
        /// ES/EN significa que alguien copió la columna en vez de traducirla.
        /// </summary>
        private static readonly HashSet<string> IdenticalByDesign = new HashSet<string>
        {
            "unlock.class.berserker.name",
            "unlock.class.gambler.name",
            "combo.generala.name",
            "combo.full_house.name",
            "menu.tutorial",
            // "Reroll" / "Roll" son los términos que el HUD ya venía mostrando sin
            // traducir; lo único traducible de estas dos entries es el "-1 {ENERGY}",
            // que es idéntico en los dos idiomas por ser icono + número.
            UiTextKeys.RerollPaid,
            UiTextKeys.ChainRollPaid,
            // "Combo" se escribe igual en los dos idiomas y es el término que el juego ya
            // usa sin traducir en el resto del HUD.
            Rollgeon.UI.HUD.Contract.ContractTextKeys.HeaderName,
            // Los tiers del cofre usan los nombres estándar de rareza del juego,
            // sin traducir (mismo criterio que "Reroll"/"Combo").
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityCommon,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityUncommon,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityRare,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityLegendary,
        };

        private static readonly string[] Collections = { "Content", "UI" };

        [Test]
        public void test_localization_every_key_has_a_value_in_both_locales()
        {
            // Arrange
            var empties = new List<string>();

            // Act
            foreach (string collectionName in Collections)
            {
                var collection = RequireCollection(collectionName);
                foreach (var shared in collection.SharedData.Entries)
                {
                    if (string.IsNullOrEmpty(ValueOf(collection, EsCode, shared.Key)))
                        empties.Add($"{collectionName}/{shared.Key} [es]");
                    if (string.IsNullOrEmpty(ValueOf(collection, EnCode, shared.Key)))
                        empties.Add($"{collectionName}/{shared.Key} [en]");
                }
            }

            // Assert
            Assert.IsEmpty(empties, "Entries sin texto:\n" + string.Join("\n", empties));
        }

        [Test]
        public void test_localization_no_key_repeats_the_spanish_text_in_english()
        {
            // Arrange
            var untranslated = new List<string>();

            // Act
            foreach (string collectionName in Collections)
            {
                var collection = RequireCollection(collectionName);
                foreach (var shared in collection.SharedData.Entries)
                {
                    if (IdenticalByDesign.Contains(shared.Key)) continue;

                    string es = ValueOf(collection, EsCode, shared.Key);
                    string en = ValueOf(collection, EnCode, shared.Key);
                    if (!string.IsNullOrEmpty(es) && es == en)
                        untranslated.Add($"{collectionName}/{shared.Key} = \"{es}\"");
                }
            }

            // Assert
            Assert.IsEmpty(untranslated,
                "Keys con ES == EN (traducir, o sumar a IdenticalByDesign si es un nombre propio):\n"
                + string.Join("\n", untranslated));
        }

        [Test]
        public void test_localization_every_tutorial_key_exists_in_the_ui_table()
        {
            // Arrange
            var collection = RequireCollection("UI");

            // Act
            var missing = TutorialTextKeys.All
                .Where(key => collection.SharedData.GetEntry(key) == null)
                .ToList();

            // Assert
            Assert.IsEmpty(missing,
                "Keys de TutorialTextKeys sin entry en la tabla UI:\n" + string.Join("\n", missing));
        }

        [Test]
        public void test_localization_every_hud_chrome_key_exists_in_the_ui_table()
        {
            // Arrange
            var collection = RequireCollection("UI");

            // Act
            var missing = UiTextKeys.All
                .Where(key => collection.SharedData.GetEntry(key) == null)
                .ToList();

            // Assert
            Assert.IsEmpty(missing,
                "Keys de UiTextKeys sin entry en la tabla UI:\n" + string.Join("\n", missing));
        }

        [Test]
        public void test_localization_every_build_help_key_exists_in_the_ui_table()
        {
            // Arrange
            var collection = RequireCollection("UI");

            // Act
            var missing = BuildHelpTextKeys.All
                .Where(key => collection.SharedData.GetEntry(key) == null)
                .ToList();

            // Assert
            Assert.IsEmpty(missing,
                "Keys de BuildHelpTextKeys sin entry en la tabla UI:\n" + string.Join("\n", missing));
        }

        /// <summary>
        /// El <c>{ENERGY}</c> es lo que <c>IconSpriteTags</c> convierte en el glifo del
        /// atlas. Una traducción que lo pierda (o lo traduzca) deja el costo sin icono,
        /// que es exactamente el bug que estos textos vinieron a arreglar.
        /// </summary>
        [Test]
        public void test_localization_cost_keys_keep_the_energy_placeholder_in_both_locales()
        {
            // Arrange
            var collection = RequireCollection("UI");
            var costKeys = new[] { UiTextKeys.RerollPaid, UiTextKeys.ChainRollPaid };
            var broken = new List<string>();

            // Act
            foreach (string key in costKeys)
            {
                foreach (string locale in new[] { EsCode, EnCode })
                {
                    string value = ValueOf(collection, locale, key);
                    if (value == null || !value.Contains("{ENERGY}"))
                        broken.Add($"UI/{key} [{locale}] = \"{value}\"");
                }
            }

            // Assert
            Assert.IsEmpty(broken,
                "Keys de costo que perdieron el token {ENERGY}:\n" + string.Join("\n", broken));
        }

        [Test]
        public void test_localization_every_unlock_has_name_desc_and_hint()
        {
            // Arrange
            var collection = RequireCollection("Content");
            var definitions = AssetDatabase.FindAssets($"t:{nameof(UnlockDefinitionSO)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnlockDefinitionSO>)
                .Where(def => def != null)
                .ToList();
            Assume.That(definitions, Is.Not.Empty, "No se encontró ninguna UnlockDefinitionSO en el proyecto.");
            var missing = new List<string>();

            // Act
            foreach (var def in definitions)
            {
                // La pista es lo único visible mientras el unlock está bloqueado — sin
                // esta key la pantalla cae al texto autor en español (bug original).
                foreach (string suffix in new[] { ".name", ".desc", ".hint" })
                {
                    if (collection.SharedData.GetEntry(def.UnlockId + suffix) == null)
                        missing.Add(def.UnlockId + suffix);
                }
            }

            // Assert
            Assert.IsEmpty(missing, "Keys de unlocks ausentes en Content:\n" + string.Join("\n", missing));
        }

        private static StringTableCollection RequireCollection(string name)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name);
            Assert.IsNotNull(collection, $"No existe la String Table Collection '{name}'.");
            return collection;
        }

        private static string ValueOf(StringTableCollection collection, string localeCode, string key)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            Assert.IsNotNull(table, $"'{collection.TableCollectionName}' no tiene tabla para '{localeCode}'.");

            var entry = table.GetEntry(key);
            return entry != null ? entry.Value : null;
        }
    }
}
