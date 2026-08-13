using System;
using Patterns;
using Rollgeon.Achievements;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Rollgeon.Steam
{
    /// <summary>
    /// Inicializa SteamAPI durante el bootstrap global (Feature#0019). Va en la
    /// lista <c>ExtraServices</c> de <c>ServiceBootstrap.asset</c>.
    /// <para>
    /// Registra <see cref="ISteamService"/> SIEMPRE (aunque el Init falle, con
    /// <c>Available == false</c>) para que consola y diagnósticos tengan algo que
    /// reportar. El juego nunca depende de que Steam esté: cliente cerrado o DLL
    /// ausente degradan a warning único.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SteamServiceBootstrap : IPreloadableService, IDisposable
    {
        /// <summary>Temprano: otros servicios pueden consultar ISteamService en su Register().</summary>
        public const int DefaultPriority = 10;

        // Estáticos: el ciclo Init/Shutdown de SteamAPI es por-proceso, no
        // por-instancia (y la instancia viene deserializada por Odin).
        private static bool s_initialized;
        private static SteamService s_service;

        [NonSerialized] private bool _registered;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <summary>SteamAPI inicializado en esta sesión de juego.</summary>
        internal static bool IsInitialized => s_initialized;

        // Fast-enter-playmode (domain reload off): el estado por-proceso puede
        // quedar colgado de la sesión de Play anterior.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_initialized = false;
            s_service = null;
        }

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            if (_registered) return;
            _registered = true;

#if !DISABLESTEAMWORKS
            var service = new SteamService();
            s_service = service;
            ServiceLocator.AddService<ISteamService>(service, ServiceScope.Global);

            if (!ServiceLocator.TryGetService<SteamConfigSO>(out var config) || config == null)
            {
                Debug.LogWarning("[Steam] SteamConfigSO no está en SettingsAssets del bootstrap — Steam deshabilitado.");
                return;
            }

            try
            {
#if !UNITY_EDITOR
                // Lanzado fuera de Steam sin steam_appid.txt junto al exe: Steam
                // relanza el juego desde el cliente y este proceso debe morir.
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(config.AppId)))
                {
                    Application.Quit();
                    return;
                }
#endif
                s_initialized = SteamAPI.Init();
            }
            catch (DllNotFoundException e)
            {
                Debug.LogWarning($"[Steam] steam_api64.dll no disponible — Steam deshabilitado. ({e.Message})");
                return;
            }

            if (!s_initialized)
            {
                Debug.LogWarning(
                    "[Steam] SteamAPI.Init() falló — ¿cliente de Steam cerrado, cuenta sin la app, " +
                    "o steam_appid.txt ausente? El juego sigue sin Steam.");
                return;
            }

            service.Available = true;

            var pump = new GameObject("[SteamCallbackPump]");
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<SteamCallbackPump>();

            Debug.Log($"[Steam] Init OK — AppId {config.AppId}, usuario '{service.PlayerName}'.");
#else
            Debug.LogWarning("[Steam] Compilado con DISABLESTEAMWORKS — Steam deshabilitado.");
#endif
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// Cierra la sesión de SteamAPI. Idempotente — la llaman el pump
        /// (quit / salida de Play Mode) y <see cref="Dispose"/>.
        /// </summary>
        internal static void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (!s_initialized) return;
            s_initialized = false;

            if (s_service != null)
            {
                s_service.Available = false;
            }

            SteamAPI.Shutdown();
#endif
        }
    }
}
