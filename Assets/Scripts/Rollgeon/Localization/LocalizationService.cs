using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Implementación de <see cref="ILocalizationService"/> sobre el package oficial
    /// <c>com.unity.localization</c>. Todo el estado real (locale seleccionado,
    /// persistencia) vive en <c>LocalizationSettings</c>; esta clase solo expone una
    /// API estable y reemite el evento de cambio de locale.
    /// </summary>
    public sealed class LocalizationService : ILocalizationService, IDisposable
    {
        /// <inheritdoc />
        public event Action LanguageChanged;

        public LocalizationService()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        /// <inheritdoc />
        public Locale Current => LocalizationSettings.SelectedLocale;

        /// <inheritdoc />
        public string CurrentCode
        {
            get
            {
                var locale = Current;
                return locale != null ? locale.Identifier.Code : null;
            }
        }

        /// <inheritdoc />
        public void SetLanguage(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode)) return;

            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCode));
            if (locale == null)
            {
                Debug.LogWarning($"[Localization] Locale '{localeCode}' no está entre los locales disponibles del proyecto.");
                return;
            }

            LocalizationSettings.SelectedLocale = locale;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            LanguageChanged?.Invoke();
        }

        public void Dispose()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }
    }
}
