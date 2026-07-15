using System;
using NUnit.Framework;
using Patterns;
using UnityEngine;
using UnityEngine.Localization;

namespace Rollgeon.Localization.Tests
{
    /// <summary>
    /// Verifica que los handlers públicos del selector delegan en
    /// <see cref="ILocalizationService"/> con el código de idioma correcto. Usa un fake
    /// registrado en el <see cref="ServiceLocator"/> — sin Play Mode ni package real.
    /// </summary>
    public class LanguageSelectorTests
    {
        private sealed class FakeLocalizationService : ILocalizationService
        {
            public string LastCode;
            public Locale Current => null;
            public string CurrentCode => LastCode;
            public event Action LanguageChanged;

            public void SetLanguage(string localeCode)
            {
                LastCode = localeCode;
                LanguageChanged?.Invoke();
            }
        }

        [Test]
        public void Select_buttons_delegate_correct_codes_to_service()
        {
            // Arrange
            var fake = new FakeLocalizationService();
            ServiceLocator.AddService<ILocalizationService>(fake);
            var go = new GameObject("LanguageSelectorUnderTest");
            var selector = go.AddComponent<LanguageSelector>();

            try
            {
                // Act + Assert
                selector.SelectSpanish();
                Assert.AreEqual("es", fake.LastCode);

                selector.SelectEnglish();
                Assert.AreEqual("en", fake.LastCode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ServiceLocator.RemoveService<ILocalizationService>();
            }
        }
    }
}
