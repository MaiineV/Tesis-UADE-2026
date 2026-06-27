using Patterns;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.DevConsole.UI
{
    /// <summary>
    /// Auto-instala la DevConsole al entrar en Play. En editor o development builds crea el overlay
    /// persistente (DontDestroyOnLoad) disponible en todas las escenas; en release builds el guard #if
    /// elimina el hook → cero footprint y cero cheats. No requiere wiring de escena.
    /// </summary>
    public static class DevConsoleInstaller
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Idempotente frente a fast-enter-playmode (sin domain reload) y multi-escena.
            if (Object.FindObjectOfType<DevConsoleUI>() != null) return;

            // Estado consumido por DiceRoller para riggear tiradas (setdiceroll).
            if (!ServiceLocator.HasService<RiggedRollState>())
                ServiceLocator.AddService<RiggedRollState>(new RiggedRollState(), ServiceScope.Global);

            var go = new GameObject("DevConsole");
            go.AddComponent<DevConsoleUI>();
            Object.DontDestroyOnLoad(go);
            Debug.Log("[DevConsole] instalada (Editor/DevBuild). Abrí con ` (backquote) o F1.");
        }
#endif
    }
}
