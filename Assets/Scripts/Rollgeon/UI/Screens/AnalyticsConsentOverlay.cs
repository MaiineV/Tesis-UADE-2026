using Patterns;
using Rollgeon.Analytics;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Popup de consentimiento de telemetría (Feature#0029, opt-in GDPR). Lo
    /// abre <see cref="MainMenuScreen"/> en la primera ejecución (sin decisión
    /// previa); la elección persiste en PlayerPrefs vía
    /// <see cref="IAnalyticsConsentService"/> y se puede cambiar después con el
    /// toggle "Telemetría" del menú.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject hijo del Canvas en <c>01_MainMenu</c>, arranca
    /// desactivado (mismo esquema que <c>PauseMenuOverlay</c>). Ver
    /// <c>docs/setup/unity-analytics-setup.md</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Analytics Consent Overlay")]
    public class AnalyticsConsentOverlay : BaseScreen
    {
        private const string LogPrefix = "[AnalyticsConsentOverlay] ";
        public const string ScreenId = "AnalyticsConsent";

        // ---- Inspector refs ----
        [Title("Overlay — Analytics Consent")]
        [Tooltip("Título del popup. Opcional — el texto se setea por código (tabla UI).")]
        [SerializeField] private TMP_Text _titleLabel;

        [Tooltip("Cuerpo del popup (qué datos se envían y por qué). Opcional.")]
        [SerializeField] private TMP_Text _bodyLabel;

        [Required("Arrastrar el Button de aceptar.")]
        [SerializeField] private Button _acceptButton;

        [Required("Arrastrar el Button de rechazar.")]
        [SerializeField] private Button _declineButton;

        [Tooltip("Button que abre la política de privacidad de Unity. Opcional.")]
        [SerializeField] private Button _privacyButton;

        [Tooltip("Labels de los botones — opcionales, se localizan por código (tabla UI).")]
        [SerializeField] private TMP_Text _acceptLabel;
        [SerializeField] private TMP_Text _declineLabel;
        [SerializeField] private TMP_Text _privacyLabel;

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            RefreshLabels();
            LocalizationRefresh.Subscribe(RefreshLabels);

            if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_declineButton != null) _declineButton.onClick.AddListener(OnDeclineClicked);
            if (_privacyButton != null) _privacyButton.onClick.AddListener(OnPrivacyClicked);
        }

        protected override void OnPopped()
        {
            if (_acceptButton != null) _acceptButton.onClick.RemoveListener(OnAcceptClicked);
            if (_declineButton != null) _declineButton.onClick.RemoveListener(OnDeclineClicked);
            if (_privacyButton != null) _privacyButton.onClick.RemoveListener(OnPrivacyClicked);

            LocalizationRefresh.Unsubscribe(RefreshLabels);
        }

        // Labels code-set (docs/setup/localization-setup.md §Labels code-set):
        // sin LocalizeStringEvent — menos wiring de escena y un solo carril.
        private void RefreshLabels()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = LocalizedContent.Ui(
                    "menu.analytics_consent_title", "Datos de juego anónimos");
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = LocalizedContent.Ui(
                    "menu.analytics_consent_body",
                    "¿Nos ayudás a balancear el juego? Enviamos datos anónimos de tus " +
                    "partidas (combates, oro, combos). Sin datos personales. Podés " +
                    "cambiarlo cuando quieras desde el menú.");
            }

            if (_acceptLabel != null)
            {
                _acceptLabel.text = LocalizedContent.Ui("menu.analytics_consent_accept", "Aceptar");
            }

            if (_declineLabel != null)
            {
                _declineLabel.text = LocalizedContent.Ui("menu.analytics_consent_decline", "Rechazar");
            }

            if (_privacyLabel != null)
            {
                _privacyLabel.text = LocalizedContent.Ui("menu.analytics_consent_privacy", "Política de privacidad");
            }
        }

        private void OnAcceptClicked() => Decide(granted: true);

        private void OnDeclineClicked() => Decide(granted: false);

        private void Decide(bool granted)
        {
            if (ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent) && consent != null)
            {
                consent.SetConsent(granted);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IAnalyticsConsentService no está registrado — la decisión no se persiste.", this);
            }

            Close();
        }

        private void OnPrivacyClicked()
        {
            var url = ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent) && consent != null
                ? consent.PrivacyUrl
                : "https://unity.com/legal/privacy-policy";
            Application.OpenURL(url);
        }

        private void Close()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopOverlay();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no está registrado — no se puede cerrar el overlay.", this);
            }
        }
    }
}
