using Rollgeon.EditorTools.Localization;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Analytics
{
    /// <summary>
    /// Setup de las entries de localización de la UI de telemetría
    /// (Feature#0029): toggle del main menu + popup de consentimiento.
    /// Idempotente — reejecutar sobrescribe valores sin duplicar keys.
    /// </summary>
    public static class AnalyticsSetupTools
    {
        [MenuItem("Rollgeon/Analytics/Setup Localization Entries")]
        public static void SetupLocalizationEntries()
        {
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_on",
                "Telemetría: ON", "Analytics: ON");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_off",
                "Telemetría: OFF", "Analytics: OFF");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_consent_title",
                "Datos de juego anónimos", "Anonymous gameplay data");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_consent_body",
                "¿Nos ayudás a balancear el juego? Enviamos datos anónimos de tus partidas " +
                "(combates, oro, combos). Sin datos personales. Podés cambiarlo cuando " +
                "quieras desde el menú.",
                "Help us balance the game? We send anonymous gameplay data (combats, gold, " +
                "combos). No personal data. You can change this anytime from the menu.");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_consent_accept",
                "Aceptar", "Accept");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_consent_decline",
                "Rechazar", "Decline");
            LocalizationSetupTools.UpsertEntry("UI", "menu.analytics_consent_privacy",
                "Política de privacidad", "Privacy policy");

            AssetDatabase.SaveAssets();
            Debug.Log("[AnalyticsSetupTools] 7 entries de la tabla UI creadas/actualizadas (menu.analytics_*).");
        }
    }
}
