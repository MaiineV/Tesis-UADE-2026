using UnityEngine;

namespace Rollgeon.Analytics
{
    /// <summary>
    /// Persistencia del consentimiento de analytics (Feature#0029). Único punto
    /// del feature que toca PlayerPrefs.
    /// <para>
    /// Va en PlayerPrefs y NO en el meta save a propósito: el consentimiento es
    /// una preferencia de dispositivo/usuario y debe sobrevivir al botón
    /// "Borrar partida" (que borra meta save + run save). El SDK de UGS también
    /// guarda su estado en PlayerPrefs — por eso <c>PlayerPrefs.DeleteAll()</c>
    /// está prohibido en el repo.
    /// </para>
    /// </summary>
    public static class AnalyticsPrefs
    {
        public const string ConsentDecidedKey = "rollgeon.analytics.consent_decided";
        public const string ConsentGrantedKey = "rollgeon.analytics.consent_granted";

        /// <summary>El jugador ya tomó una decisión (aceptar o rechazar) alguna vez.</summary>
        public static bool HasDecision => PlayerPrefs.GetInt(ConsentDecidedKey, 0) == 1;

        /// <summary>Decisión vigente; <c>false</c> también cuando nunca decidió.</summary>
        public static bool IsGranted => PlayerPrefs.GetInt(ConsentGrantedKey, 0) == 1;

        public static void SaveConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentDecidedKey, 1);
            PlayerPrefs.SetInt(ConsentGrantedKey, granted ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Vuelve al estado "nunca preguntado" — DevConsole y tests.</summary>
        public static void ClearDecision()
        {
            PlayerPrefs.DeleteKey(ConsentDecidedKey);
            PlayerPrefs.DeleteKey(ConsentGrantedKey);
        }
    }
}
