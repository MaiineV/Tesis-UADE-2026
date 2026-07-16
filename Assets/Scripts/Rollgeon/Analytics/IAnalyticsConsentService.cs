namespace Rollgeon.Analytics
{
    /// <summary>
    /// Lo que la UI necesita del consentimiento de analytics (Feature#0029):
    /// popup de primera ejecución y toggle del main menu. Régimen opt-in
    /// universal (GDPR) — sin decisión explícita no se envía nada.
    /// </summary>
    public interface IAnalyticsConsentService
    {
        /// <summary>El jugador ya aceptó o rechazó alguna vez — gobierna si se muestra el popup.</summary>
        bool HasDecision { get; }

        /// <summary>Decisión vigente. <c>false</c> tanto para "rechazó" como para "nunca decidió".</summary>
        bool IsGranted { get; }

        /// <summary>Persiste la decisión y la aplica al SDK si ya inicializó.</summary>
        void SetConsent(bool granted);

        /// <summary>URL de la política de privacidad para el botón del popup.</summary>
        string PrivacyUrl { get; }

        /// <summary>Pide borrar los datos ya subidos (GDPR). <c>false</c> si el SDK no está listo.</summary>
        bool TryRequestDataDeletion();
    }
}
