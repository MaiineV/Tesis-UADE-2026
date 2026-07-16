using Patterns;

namespace Rollgeon.Analytics
{
    /// <summary>
    /// Implementación concreta de <see cref="IAnalyticsConsentService"/>
    /// (Feature#0029). Clase plana — la registra <c>AnalyticsTrackerService</c>
    /// en su <c>Register()</c>. Persiste en <see cref="AnalyticsPrefs"/> y
    /// resuelve <see cref="IAnalyticsGateway"/> lazy: si el SDK todavía no
    /// inicializó, la decisión queda en prefs y el bootstrap UGS la relee al
    /// completar su init.
    /// </summary>
    public sealed class AnalyticsConsentService : IAnalyticsConsentService
    {
        // Fallback cuando el SDK no está (sin package resuelto, init fallido):
        // el botón de privacidad del popup tiene que abrir algo siempre.
        private const string FallbackPrivacyUrl = "https://unity.com/legal/privacy-policy";

        /// <inheritdoc />
        public bool HasDecision => AnalyticsPrefs.HasDecision;

        /// <inheritdoc />
        public bool IsGranted => AnalyticsPrefs.IsGranted;

        /// <inheritdoc />
        public void SetConsent(bool granted)
        {
            AnalyticsPrefs.SaveConsent(granted);

            var gateway = GetGateway();
            if (gateway != null && gateway.Initialized)
            {
                gateway.ApplyConsent(granted);
            }
        }

        /// <inheritdoc />
        public string PrivacyUrl
        {
            get
            {
                var url = GetGateway()?.PrivacyUrl;
                return string.IsNullOrEmpty(url) ? FallbackPrivacyUrl : url;
            }
        }

        /// <inheritdoc />
        public bool TryRequestDataDeletion()
        {
            var gateway = GetGateway();
            return gateway != null && gateway.Initialized && gateway.TryRequestDataDeletion();
        }

        private static IAnalyticsGateway GetGateway() =>
            ServiceLocator.TryGetService<IAnalyticsGateway>(out var gateway) ? gateway : null;
    }
}
