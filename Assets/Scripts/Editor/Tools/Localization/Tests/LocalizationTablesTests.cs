using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities.Traits;
using Rollgeon.Meta;
using Rollgeon.Tutorial;
using Rollgeon.UI;
using Rollgeon.UI.Help;
using Rollgeon.Tiles;
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
            // Tabs y canal de audio: "General", "Audio" y "Master" se escriben
            // igual en los dos idiomas.
            "menu.tab_general",
            "menu.tab_audio",
            "menu.audio_master",
            // "Portal" es portal en los dos idiomas (Casillas Especiales).
            "tile.portal.name",
            // "Combo" se escribe igual en los dos idiomas y es el término que el juego ya
            // usa sin traducir en el resto del HUD.
            Rollgeon.UI.HUD.Contract.ContractTextKeys.HeaderName,
            // Los tiers del cofre usan los nombres estándar de rareza del juego,
            // sin traducir (mismo criterio que "Reroll"/"Combo").
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityCommon,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityUncommon,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityRare,
            Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityLegendary,
            // BUG-041: sufijo de multiplicador de combo (" × 1.5") — símbolo + número
            // puro, sin palabras que traducir (mismo criterio que "Combo").
            "tooltip.effect.combo.multiplier_suffix",
            // "Melee" y "Support" son los términos que el equipo ya usa en castellano para
            // hablar de las familias, igual que "Combo" y "Reroll". "Rango"/"Ranged" sí se
            // traduce, y por eso no está acá.
            EnemyArchetypeKeys.Melee,
            EnemyArchetypeKeys.Support,
            // "Combo" otra vez (tipo de ataque), y el formato del título con tipo: separador
            // y orden, sin palabras que traducir.
            Rollgeon.UI.HUD.Status.AttackKindTextKeys.ComboAttack,
            Rollgeon.UI.HUD.Status.AttackKindTextKeys.TitleFormat,
        };

        /// <summary>
        /// Keys que existen vacías a propósito. Una descripción de intención vacía es lo que
        /// pide una tarjeta de solo título: la entry sigue estando para poder llenarla desde la
        /// tabla, sin tocar código, el día que esa tarjeta quiera decir algo más.
        /// </summary>
        private static readonly HashSet<string> EmptyByDesign = new HashSet<string>
        {
            // "Detonar la bomba" ya dice qué pasa, y el badge dice cuántos turnos faltan.
            AIIntentTextKeys.BombBlast + ".desc",
            // "Te dispara" más el número de la tarjeta ya lo dicen entero, y "desde lejos" lo
            // dice la fila de familia del panel.
            AIIntentTextKeys.RangedShot + ".desc",
            // Tarjetas de solo título: el número dice cuánto, y las casillas marcadas del golpe
            // telegrafiado ya se ven en el paño.
            AIIntentTextKeys.Telegraph + ".desc",
            AIIntentTextKeys.Attack + ".desc",
            // "Ambiental" en el título de un ataque no califica nada que el jugador pueda usar.
            Rollgeon.UI.HUD.Status.AttackKindTextKeys.Environmental,
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
                    if (EmptyByDesign.Contains(shared.Key)) continue;

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
        public void test_localization_every_archetype_key_exists_in_the_ui_table()
        {
            // Arrange
            var collection = RequireCollection("UI");

            // Act
            var missing = EnemyArchetypeKeys.All
                .Where(key => collection.SharedData.GetEntry(key) == null)
                .ToList();

            // Assert
            Assert.IsEmpty(missing,
                "Keys de EnemyArchetypeKeys sin entry en la tabla UI:\n" + string.Join("\n", missing));
        }

        [Test]
        public void test_localization_every_attack_kind_key_exists_in_the_ui_table()
        {
            // Arrange
            var collection = RequireCollection("UI");

            // Act
            var missing = Rollgeon.UI.HUD.Status.AttackKindTextKeys.All
                .Where(key => collection.SharedData.GetEntry(key) == null)
                .ToList();

            // Assert
            Assert.IsEmpty(missing,
                "Keys de AttackKindTextKeys sin entry en la tabla UI — el título de la tarjeta " +
                "de próximo turno cae al texto de autor:\n" + string.Join("\n", missing));
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

        /// <summary>
        /// Pares de casillas que comparten clave a sabiendas y todavía no se separaron. Sacar una
        /// línea de acá el día que la casilla reciba su propio texto.
        /// </summary>
        /// <remarks>
        /// 2026-08-26 — Los pinchos del Cajero cobran 20 contra los 12 de los genéricos y se
        /// titulan "Pinchos" igual que ellos. Es el mismo bug que tenía el fuego de bomba del
        /// Croupier; queda afuera de la rama del hover porque es otra pelea.
        /// </remarks>
        private static readonly HashSet<string> SharedTileKeysByDesign = new HashSet<string>
        {
            "tile.spikes",
        };

        [Test]
        public void test_localization_tiles_that_charge_differently_do_not_share_a_key()
        {
            // Arrange — el tooltip de una casilla se titula con su NameKey y sus números salen de
            // la definición. Dos casillas con la misma clave y distinto precio se leen como la
            // misma cosa cobrando cualquier número.
            var byKey = new Dictionary<string, List<SpecialTileDefinitionSO>>();
            foreach (string guid in AssetDatabase.FindAssets("t:SpecialTileDefinitionSO"))
            {
                var def = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.NameKey)) continue;

                if (!byKey.TryGetValue(def.NameKey, out var group))
                    byKey[def.NameKey] = group = new List<SpecialTileDefinitionSO>();
                group.Add(def);
            }

            // Act
            var collisions = new List<string>();
            foreach (var pair in byKey)
            {
                if (pair.Value.Count < 2 || SharedTileKeysByDesign.Contains(pair.Key)) continue;

                var first = pair.Value[0];
                foreach (var other in pair.Value)
                {
                    if (other.EnterDamage == first.EnterDamage
                        && other.TurnStartDamage == first.TurnStartDamage
                        && other.HealAmount == first.HealAmount) continue;

                    collisions.Add($"{pair.Key}: " + string.Join(", ", pair.Value.Select(
                        d => $"{d.name} ({d.EnterDamage}/{d.TurnStartDamage})")));
                    break;
                }
            }

            // Assert
            Assert.IsEmpty(collisions,
                "Casillas con precios distintos compartiendo texto — la que no es dueña de la clave " +
                "se titula con el nombre de la otra:\n" + string.Join("\n", collisions));
        }

        /// <summary>
        /// Las intenciones se escriben en la tarjeta que sale al pasarle el mouse a un enemigo.
        /// Las cuatro vivieron una rama entera sin entry: salían con el texto de autor, en
        /// español, con el juego corriendo en inglés, y ningún test lo veía.
        /// </summary>
        [Test]
        public void test_localization_every_intent_key_exists_in_the_content_table()
        {
            // Arrange
            var collection = RequireCollection("Content");

            // Act
            var missing = new List<string>();
            foreach (string key in AIIntentTextKeys.All)
            {
                foreach (string suffix in new[] { ".name", ".desc" })
                {
                    if (collection.SharedData.GetEntry(key + suffix) == null)
                        missing.Add(key + suffix);
                }
            }

            // Assert
            Assert.IsEmpty(missing,
                "Keys de intenciones ausentes en Content — la tarjeta del enemigo cae al texto " +
                "de autor y sale en español en cualquier idioma:\n" + string.Join("\n", missing));
        }

        [Test]
        public void test_localization_every_tile_key_exists_in_the_content_table()
        {
            // Arrange
            var collection = RequireCollection("Content");

            // Act — sin entrada, LocalizedContent.Description devuelve vacío y el tooltip pierde la
            // descripción entera sin avisar; el nombre cae al DisplayName de editor.
            var missing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:SpecialTileDefinitionSO"))
            {
                var def = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.NameKey)) continue;

                foreach (string suffix in new[] { ".name", ".desc" })
                {
                    if (collection.SharedData.GetEntry(def.NameKey + suffix) == null)
                        missing.Add(def.NameKey + suffix);
                }
            }

            // Assert
            Assert.IsEmpty(missing, "Keys de casillas ausentes en Content:\n" + string.Join("\n", missing));
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
