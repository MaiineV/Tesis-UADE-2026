namespace Rollgeon.Analytics
{
    /// <summary>
    /// Superficie de control del SDK de analytics, sin exponer tipos UGS
    /// (Feature#0029). La consume <see cref="AnalyticsConsentService"/> para
    /// aplicar la decisión del jugador al SDK real; en tests se fakea.
    /// </summary>
    public interface IAnalyticsGateway
    {
        /// <summary>UnityServices terminó de inicializar (async, puede no llegar nunca sin red/link).</summary>
        bool Initialized { get; }

        /// <summary>
        /// Aplica el consentimiento al SDK (Granted/Denied). Solo tiene efecto
        /// con <see cref="Initialized"/> — el caller decide reintentarlo después.
        /// </summary>
        void ApplyConsent(bool granted);

        /// <summary>URL de la política de privacidad de Unity para mostrar al jugador.</summary>
        string PrivacyUrl { get; }

        /// <summary>Pide el borrado de datos del jugador al backend. <c>false</c> si el SDK no está listo.</summary>
        bool TryRequestDataDeletion();
    }
}
