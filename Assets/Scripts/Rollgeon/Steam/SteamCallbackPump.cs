using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Rollgeon.Steam
{
    /// <summary>
    /// Ticker de Steamworks (Feature#0019). El proyecto no tiene game-loop global,
    /// así que <see cref="SteamServiceBootstrap"/> spawnea este MonoBehaviour en un
    /// GO DontDestroyOnLoad cuando el Init fue exitoso — sin RunCallbacks por frame
    /// los <c>Callback&lt;T&gt;</c> no despachan y el cliente no procesa eventos.
    /// </summary>
    internal sealed class SteamCallbackPump : MonoBehaviour
    {
        private void Update()
        {
#if !DISABLESTEAMWORKS
            if (SteamServiceBootstrap.IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
#endif
        }

        private void OnApplicationQuit()
        {
            SteamServiceBootstrap.Shutdown();
        }

        // En editor, salir de Play Mode destruye el DDOL sin garantizar
        // OnApplicationQuit — respaldo para no dejar la sesión colgada.
        private void OnDestroy()
        {
            SteamServiceBootstrap.Shutdown();
        }
    }
}
