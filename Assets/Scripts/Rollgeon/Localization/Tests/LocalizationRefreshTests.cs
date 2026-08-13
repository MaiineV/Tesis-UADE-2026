using NUnit.Framework;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Rollgeon.Localization.Tests
{
    /// <summary>
    /// Regresión del panel de opciones: cambiar el idioma dejaba los labels seteados
    /// por código con el texto anterior porque nadie escuchaba el cambio de locale.
    /// Acá se cubre el carril de notificación; que cada vista se suscriba y re-renderice
    /// es responsabilidad de la vista.
    /// </summary>
    public class LocalizationRefreshTests
    {
        private Locale _original;

        [SetUp]
        public void SetUp()
        {
            _original = LocalizationSettings.SelectedLocale;
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationSettings.SelectedLocale = _original;
        }

        [Test]
        public void test_localization_refresh_subscribed_handler_runs_on_language_change()
        {
            // Arrange
            int calls = 0;
            System.Action handler = () => calls++;
            LocalizationRefresh.Subscribe(handler);

            // Act
            SelectOtherLocale();

            // Assert
            LocalizationRefresh.Unsubscribe(handler);
            Assert.AreEqual(1, calls, "El handler suscrito tiene que correr al cambiar el locale.");
        }

        [Test]
        public void test_localization_refresh_subscribing_twice_runs_the_handler_once()
        {
            // Arrange
            int calls = 0;
            System.Action handler = () => calls++;
            LocalizationRefresh.Subscribe(handler);
            LocalizationRefresh.Subscribe(handler);

            // Act
            SelectOtherLocale();

            // Assert
            LocalizationRefresh.Unsubscribe(handler);
            Assert.AreEqual(1, calls, "Suscribir dos veces el mismo delegate no debe duplicar la llamada.");
        }

        [Test]
        public void test_localization_refresh_unsubscribed_handler_does_not_run()
        {
            // Arrange
            int calls = 0;
            System.Action handler = () => calls++;
            LocalizationRefresh.Subscribe(handler);
            LocalizationRefresh.Unsubscribe(handler);

            // Act
            SelectOtherLocale();

            // Assert
            Assert.AreEqual(0, calls, "Un handler dado de baja no debe correr — sería una vista ya cerrada.");
        }

        /// <summary>
        /// Cambia a cualquier locale distinto del activo: el evento del package solo
        /// dispara cuando el locale realmente cambia.
        /// </summary>
        private static void SelectOtherLocale()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            Assume.That(locales.Count, Is.GreaterThan(1), "El proyecto necesita 2+ locales para este test.");

            var current = LocalizationSettings.SelectedLocale;
            foreach (var locale in locales)
            {
                if (locale == current) continue;
                LocalizationSettings.SelectedLocale = locale;
                return;
            }
        }
    }
}
