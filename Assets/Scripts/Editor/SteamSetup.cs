#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Achievements;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Steam;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Setup one-click de la integración Steam (Feature#0019): crea SteamConfig.asset
    /// si falta (AppId + logros de ejemplo) y lo cablea en ServiceBootstrap.asset —
    /// SettingsAssets += config, ExtraServices += SteamServiceBootstrap +
    /// AchievementService. Edición de objeto vivo, no YAML: Odin escribe su propia
    /// serialización al guardar. Idempotente. Menú: Rollgeon → Steam → Setup.
    /// </summary>
    public static class SteamSetup
    {
        private const string ConfigPath = "Assets/Rollgeon/SteamConfig.asset";

        [MenuItem("Rollgeon/Steam/Setup Steam Integration")]
        public static void Setup()
        {
            var config = AssetDatabase.LoadAssetAtPath<SteamConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SteamConfigSO>();
                // "test" usa el logro demo de Spacewar (480) para validar el pipeline
                // antes de publicar logros propios; "first_win" queda apuntando al
                // API name que se defina en el partner site.
                config.Achievements.Add(new AchievementDefinition
                {
                    Key = "test",
                    SteamApiName = "ACH_WIN_ONE_GAME",
                    Trigger = AchievementTrigger.Manual,
                });
                config.Achievements.Add(new AchievementDefinition
                {
                    Key = "first_win",
                    SteamApiName = "ACH_WIN_FIRST_RUN",
                    Trigger = AchievementTrigger.RunVictory,
                });
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var guids = AssetDatabase.FindAssets("t:ServiceBootstrapSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SteamSetup] No se encontró ServiceBootstrap.asset — cablear a mano.");
                return;
            }

            var bootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            if (bootstrap == null)
            {
                Debug.LogWarning("[SteamSetup] ServiceBootstrap.asset no cargó como ServiceBootstrapSO.");
                return;
            }

            var changes = new List<string>();

            bootstrap.SettingsAssets ??= new List<ScriptableObject>();
            if (!bootstrap.SettingsAssets.Contains(config))
            {
                bootstrap.SettingsAssets.Add(config);
                changes.Add("SteamConfig → SettingsAssets");
            }

            bootstrap.ExtraServices ??= new List<IPreloadableService>();
            if (!bootstrap.ExtraServices.Any(s => s is SteamServiceBootstrap))
            {
                bootstrap.ExtraServices.Add(new SteamServiceBootstrap());
                changes.Add("SteamServiceBootstrap → ExtraServices");
            }

            if (!bootstrap.ExtraServices.Any(s => s is AchievementService))
            {
                bootstrap.ExtraServices.Add(new AchievementService());
                changes.Add("AchievementService → ExtraServices");
            }

            if (changes.Count > 0)
            {
                EditorUtility.SetDirty(bootstrap);
                AssetDatabase.SaveAssets();
            }

            Debug.Log(changes.Count > 0
                ? $"[SteamSetup] Listo — {string.Join(", ", changes)}. Config: {ConfigPath} (AppId {config.AppId})."
                : $"[SteamSetup] Ya estaba cableado. Config: {ConfigPath} (AppId {config.AppId}).");
        }
    }
}
#endif
