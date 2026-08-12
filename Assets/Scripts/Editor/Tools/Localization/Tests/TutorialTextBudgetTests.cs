using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Tutorial;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.Editor.Tools.Localization.Tests
{
    /// <summary>
    /// Presupuesto de largo de los cuadros del tutorial (feedback playtest: textos
    /// larguísimos que nadie lee). El límite se valida contra la TABLA, no contra
    /// los fallbacks en código — la tabla es lo que el jugador ve.
    /// </summary>
    public class TutorialTextBudgetTests
    {
        /// <summary>Palabras máximas por cuadro. Un {0} cuenta como una palabra.</summary>
        private const int MaxWordsPerStep = 22;

        /// <summary>El footer es chrome del overlay, no un cuadro — sin presupuesto.</summary>
        private static readonly HashSet<string> Exempt = new HashSet<string>
        {
            TutorialTextKeys.ContinueFooter,
        };

        [Test]
        public void test_tutorial_no_step_text_exceeds_the_word_budget_in_either_locale()
        {
            // Arrange
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.IsNotNull(collection, "No existe la String Table Collection 'UI'.");
            var over = new List<string>();

            // Act
            foreach (string key in TutorialTextKeys.All)
            {
                if (Exempt.Contains(key)) continue;
                foreach (string locale in new[] { "es", "en" })
                {
                    string value = ValueOf(collection, locale, key);
                    if (string.IsNullOrEmpty(value)) continue; // lo cubre el test de tablas
                    int words = CountWords(value);
                    if (words > MaxWordsPerStep)
                        over.Add($"UI/{key} [{locale}] = {words} palabras: \"{value}\"");
                }
            }

            // Assert
            Assert.IsEmpty(over,
                $"Cuadros del tutorial por encima de las {MaxWordsPerStep} palabras "
                + "(acortar, o partir el paso en dos):\n" + string.Join("\n", over));
        }

        [Test]
        public void test_tutorial_no_step_text_repeats_the_continue_instruction_inline()
        {
            // Arrange — la indicación de continuar la agrega el footer del overlay;
            // repetida en el cuerpo era el bug del "Click para continuar" doble.
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.IsNotNull(collection, "No existe la String Table Collection 'UI'.");
            var offenders = new List<string>();
            var forbidden = new[] { "para continuar", "para seguir", "to continue" };

            // Act
            foreach (string key in TutorialTextKeys.All)
            {
                if (Exempt.Contains(key)) continue;
                foreach (string locale in new[] { "es", "en" })
                {
                    string value = ValueOf(collection, locale, key);
                    if (string.IsNullOrEmpty(value)) continue;
                    if (forbidden.Any(f => value.ToLowerInvariant().Contains(f)))
                        offenders.Add($"UI/{key} [{locale}] = \"{value}\"");
                }
            }

            // Assert
            Assert.IsEmpty(offenders,
                "Cuadros que duplican la indicación del footer:\n" + string.Join("\n", offenders));
        }

        /// <summary>Cuenta tokens con al menos una letra o dígito — los guiones
        /// sueltos ("—") no son palabras; "{0}" y "+50" sí.</summary>
        private static int CountWords(string text)
            => text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries)
                .Count(token => token.Any(char.IsLetterOrDigit));

        private static string ValueOf(StringTableCollection collection, string localeCode, string key)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            Assert.IsNotNull(table, $"'UI' no tiene tabla para '{localeCode}'.");
            var entry = table.GetEntry(key);
            return entry != null ? entry.Value : null;
        }
    }
}
