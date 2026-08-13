using Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Botones ESP/ING del menú principal. Cada uno cambia el idioma vía
    /// <see cref="ILocalizationService"/>; el package se encarga de refrescar todos los
    /// <c>LocalizeStringEvent</c> y de persistir la elección.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en la escena <c>01_MainMenu</c>; los dos <see cref="Button"/> se
    /// cablean por Inspector. Ver <c>docs/setup/localization-setup.md</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Localization/Language Selector")]
    public class LanguageSelector : MonoBehaviour
    {
        private const string LogPrefix = "[LanguageSelector] ";

        [Tooltip("Botón que selecciona Español.")]
        [SerializeField] private Button _spanishButton;

        [Tooltip("Botón que selecciona Inglés.")]
        [SerializeField] private Button _englishButton;

        private void OnEnable()
        {
            if (_spanishButton != null) _spanishButton.onClick.AddListener(SelectSpanish);
            if (_englishButton != null) _englishButton.onClick.AddListener(SelectEnglish);
        }

        private void OnDisable()
        {
            if (_spanishButton != null) _spanishButton.onClick.RemoveListener(SelectSpanish);
            if (_englishButton != null) _englishButton.onClick.RemoveListener(SelectEnglish);
        }

        /// <summary>Selecciona Español. Público para poder invocarse desde UnityEvents o tests.</summary>
        public void SelectSpanish() => SetLanguage("es");

        /// <summary>Selecciona Inglés. Público para poder invocarse desde UnityEvents o tests.</summary>
        public void SelectEnglish() => SetLanguage("en");

        private void SetLanguage(string code)
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc != null)
            {
                loc.SetLanguage(code);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "ILocalizationService no está registrado — " +
                                 "¿falta LocalizationServiceBootstrap en ServiceBootstrap.asset?", this);
            }
        }
    }
}
