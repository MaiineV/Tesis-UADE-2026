using Rollgeon.Achievements;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Rollgeon.Steam
{
    /// <summary>
    /// Implementación Steamworks.NET de <see cref="ISteamService"/> (Feature#0019).
    /// Instanciada y registrada por <see cref="SteamServiceBootstrap"/>; si
    /// <see cref="Available"/> es <c>false</c> (Init fallido) toda operación
    /// devuelve <c>false</c> sin tocar la API nativa.
    /// <para>
    /// SDK 1.61+ ya no tiene RequestCurrentStats: los stats del usuario local
    /// están sincronizados apenas <c>SteamAPI.Init()</c> retorna true.
    /// StoreStats tras SetAchievement es lo que persiste y dispara el toast.
    /// </para>
    /// </summary>
    public sealed class SteamService : ISteamService
    {
        /// <inheritdoc />
        public bool Available { get; internal set; }

        /// <inheritdoc />
        public string PlayerName
        {
#if !DISABLESTEAMWORKS
            get => Available ? SteamFriends.GetPersonaName() : null;
#else
            get => null;
#endif
        }

        /// <inheritdoc />
        public bool UnlockAchievement(string apiName)
        {
#if !DISABLESTEAMWORKS
            if (!Available || string.IsNullOrEmpty(apiName)) return false;
            return SteamUserStats.SetAchievement(apiName) && SteamUserStats.StoreStats();
#else
            return false;
#endif
        }

        /// <inheritdoc />
        public bool ClearAchievement(string apiName)
        {
#if !DISABLESTEAMWORKS
            if (!Available || string.IsNullOrEmpty(apiName)) return false;
            return SteamUserStats.ClearAchievement(apiName) && SteamUserStats.StoreStats();
#else
            return false;
#endif
        }

        /// <inheritdoc />
        public bool TryGetAchievementState(string apiName, out bool unlocked)
        {
#if !DISABLESTEAMWORKS
            if (Available && !string.IsNullOrEmpty(apiName))
            {
                return SteamUserStats.GetAchievement(apiName, out unlocked);
            }
#endif
            unlocked = false;
            return false;
        }
    }
}
