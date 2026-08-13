using Patterns;
using UnityEngine;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Levanta el cursor custom apenas carga la primera escena y lo mantiene
    /// vivo (DontDestroyOnLoad) para todas las escenas. Se auto-dispara con
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> — no necesita
    /// wiring de escena ni entrada en <c>ServiceBootstrap</c> (esa lista es
    /// Odin y el cursor es global always-on).
    /// </summary>
    public static class CursorBootstrap
    {
        private static CursorService _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var settings = Resources.Load<CursorSettingsSO>("Cursor/CursorSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CursorBootstrap] No se encontró Resources/Cursor/CursorSettings — " +
                                 "corré 'Rollgeon → Cursor → Setup'. Cursor custom desactivado.");
                return;
            }

            var root = new GameObject("[Cursor]");
            Object.DontDestroyOnLoad(root);

            _instance = root.AddComponent<CursorService>();
            _instance.Configure(settings);
            _instance.SetVisible(true);

            ServiceLocator.AddService<ICursorService>(_instance, ServiceScope.Global);
        }
    }
}
