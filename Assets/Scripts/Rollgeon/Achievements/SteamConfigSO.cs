using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Achievements
{
    /// <summary>
    /// Evento del juego que dispara la evaluación de un logro.
    /// </summary>
    public enum AchievementTrigger
    {
        /// <summary>Solo por código o consola (<c>steam ach unlock</c>).</summary>
        Manual,

        /// <summary>EventName.OnRunVictory — run ganada.</summary>
        RunVictory,

        /// <summary>EventName.OnPlayerDefeated — run perdida.</summary>
        RunDefeat,

        /// <summary>OnCombatEnd con Victory en un combate de sala Boss.</summary>
        BossDefeated,

        /// <summary>EventName.OnFloorCleared con índice ≥ IntParam.</summary>
        FloorCleared,

        /// <summary>EventName.OnComboCounterIncremented: combo StringParam llegó a IntParam.</summary>
        ComboCounterReached,
    }

    /// <summary>
    /// Mapeo de un logro del juego a su definición en Steamworks.
    /// El API name debe existir y estar <b>publicado</b> en el partner site,
    /// si no <c>SetAchievement</c> devuelve false.
    /// </summary>
    [Serializable]
    public sealed class AchievementDefinition
    {
        [Tooltip("Key interna del juego (para consola y de-dupe). Ej: first_win.")]
        public string Key;

        [Tooltip("API name exacto definido en Steamworks partner site. Ej: ACH_WIN_FIRST_RUN.")]
        public string SteamApiName;

        [Tooltip("Evento del juego que evalúa este logro.")]
        public AchievementTrigger Trigger = AchievementTrigger.Manual;

        [Tooltip("Parámetro string del trigger. ComboCounterReached: comboId a contar (vacío = cualquiera).")]
        public string StringParam;

        [Tooltip("Parámetro numérico del trigger. FloorCleared: índice de piso (0-based) que debe completarse. ComboCounterReached: conteo a alcanzar.")]
        public int IntParam;
    }

    /// <summary>
    /// Configuración autoral de la integración Steam (Feature#0019).
    /// Registrado en <c>ServiceBootstrapSO.SettingsAssets</c> — queda en el
    /// <c>ServiceLocator</c> global antes de que corran los ExtraServices, así
    /// <c>SteamServiceBootstrap</c> y <see cref="AchievementService"/> lo resuelven
    /// sin referencia directa al asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Steam Config", fileName = "SteamConfig")]
    public sealed class SteamConfigSO : ScriptableObject
    {
        [Tooltip("App ID de la app en Steamworks. Debe coincidir con steam_appid.txt en editor/builds de prueba.")]
        public uint AppId = 4889850;

        [Tooltip("Logros del juego mapeados a sus API names del partner site.")]
        public List<AchievementDefinition> Achievements = new List<AchievementDefinition>();

        public IReadOnlyList<AchievementDefinition> Definitions => Achievements;

        /// <summary>Busca una definición por su key interna (case-sensitive).</summary>
        public bool TryGetByKey(string key, out AchievementDefinition definition)
        {
            for (int i = 0; i < Achievements.Count; i++)
            {
                var def = Achievements[i];
                if (def != null && string.Equals(def.Key, key, StringComparison.Ordinal))
                {
                    definition = def;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
