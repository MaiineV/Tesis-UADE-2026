#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Survey;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Setup one-click del cuestionario de evento (Feature#0074): crea
    /// <c>SurveyConfig.asset</c> con las preguntas default si falta, lo cablea en
    /// <c>ServiceBootstrap.asset</c> (SettingsAssets += config, ExtraServices +=
    /// <see cref="SurveyServiceBootstrap"/>) y upsertea las keys <c>survey.*</c> de la
    /// tabla UI. Edición de objeto vivo, no YAML (Odin escribe su propia serialización).
    /// Idempotente. Menú: Rollgeon → Survey.
    /// </summary>
    public static class SurveySetup
    {
        public const string ConfigPath = "Assets/Rollgeon/SurveyConfig.asset";
        private const string UiTable = "UI";

        // (key, es, en) — chrome del overlay; las preguntas viven en el asset.
        private static readonly (string Key, string Es, string En)[] UiEntries =
        {
            ("survey.title", "¡Contanos qué te pareció!", "Tell us what you think!"),
            ("survey.subtitle", "Un minuto y seguís jugando. Nos ayuda a mejorar el juego.", "One minute and you're back. It helps us improve the game."),
            ("survey.send", "Enviar", "Send"),
            ("survey.skip", "Omitir", "Skip"),
            ("survey.raffle_optin", "Quiero participar del sorteo de keys", "I want to enter the key giveaway"),
            ("survey.email_placeholder", "Tu email (solo para el sorteo)", "Your email (giveaway only)"),
            ("survey.required_hint", "Faltan responder las preguntas marcadas.", "Please answer the highlighted questions."),
            ("survey.status_invalid_email", "Ese email no parece válido.", "That email doesn't look valid."),
            ("survey.status_saved", "¡Gracias! Respuesta guardada.", "Thanks! Response saved."),
            ("survey.status_sending", "Enviando…", "Sending…"),
            ("survey.status_sent", "¡Gracias! Respuesta enviada.", "Thanks! Response sent."),
            ("survey.status_offline", "¡Gracias! Guardada, se envía cuando haya conexión.", "Thanks! Saved, it will be sent when online."),
        };

        [MenuItem("Rollgeon/Survey/Setup Survey")]
        public static void Setup()
        {
            var changes = new List<string>();

            var config = AssetDatabase.LoadAssetAtPath<SurveyConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SurveyConfigSO>();
                SurveyConfigDefaults.Populate(config);
                AssetDatabase.CreateAsset(config, ConfigPath);
                changes.Add($"creado {ConfigPath}");
            }

            var bootstrap = FindBootstrap();
            if (bootstrap != null)
            {
                bootstrap.SettingsAssets ??= new List<ScriptableObject>();
                if (!bootstrap.SettingsAssets.Contains(config))
                {
                    bootstrap.SettingsAssets.Add(config);
                    changes.Add("SurveyConfig → SettingsAssets");
                }

                bootstrap.ExtraServices ??= new List<IPreloadableService>();
                if (!bootstrap.ExtraServices.Any(s => s is SurveyServiceBootstrap))
                {
                    bootstrap.ExtraServices.Add(new SurveyServiceBootstrap());
                    changes.Add("SurveyServiceBootstrap → ExtraServices");
                }

                EditorUtility.SetDirty(bootstrap);
            }

            int upserted = 0;
            foreach (var (key, es, en) in UiEntries)
            {
                LocalizationSetupTools.UpsertEntry(UiTable, key, es, en);
                upserted++;
            }
            changes.Add($"{upserted} keys survey.* en tabla {UiTable}");

            AssetDatabase.SaveAssets();
            Debug.Log($"[SurveySetup] Listo — {string.Join(", ", changes)}. " +
                      "Falta: pegar la URL del Apps Script en EndpointUrl y poner Canvas_Survey bajo el ScreenHost de 02_Gameplay.");
        }

        [MenuItem("Rollgeon/Survey/Validate Config")]
        public static void ValidateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SurveyConfigSO>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[SurveySetup] No existe {ConfigPath} — correr Rollgeon → Survey → Setup Survey.");
                return;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            bool ok = config.Validate(errors, warnings);

            foreach (var w in warnings) Debug.LogWarning("[SurveySetup] " + w, config);
            foreach (var e in errors) Debug.LogError("[SurveySetup] " + e, config);

            Debug.Log(ok
                ? $"[SurveySetup] Config válida (evento='{config.EventId}', piso={config.TriggerFloorIndex}, {config.Questions.Count} preguntas, {warnings.Count} warning(s))."
                : $"[SurveySetup] Config con {errors.Count} error(es) — ver arriba.", config);
        }

        [MenuItem("Rollgeon/Survey/Open Responses Folder")]
        public static void OpenResponsesFolder()
        {
            var dir = FileSurveyStore.DefaultRoot;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static ServiceBootstrapSO FindBootstrap()
        {
            var guids = AssetDatabase.FindAssets("t:ServiceBootstrapSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SurveySetup] No se encontró ServiceBootstrap.asset — cablear a mano.");
                return null;
            }

            var bootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (bootstrap == null)
            {
                Debug.LogWarning("[SurveySetup] ServiceBootstrap.asset no cargó como ServiceBootstrapSO.");
            }
            return bootstrap;
        }
    }
}
#endif
