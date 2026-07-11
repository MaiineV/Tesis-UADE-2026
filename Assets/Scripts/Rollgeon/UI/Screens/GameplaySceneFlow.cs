using System.Collections;
using Patterns;
using UnityEngine.SceneManagement;
using CoroutineHost = Rollgeon.Patterns.CoroutineHost;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Transición 01_MainMenu → 02_Gameplay compartida entre
    /// <see cref="BuildSelectionScreen"/> (run nueva) y <see cref="MainMenuScreen"/>
    /// (Continue). El caller deja armada la <c>PendingRunRequest</c> antes de llamar.
    /// </summary>
    public static class GameplaySceneFlow
    {
        public const string GameplaySceneName = "02_Gameplay";

        /// <summary>Muestra la loading screen y carga gameplay reportando progreso real.</summary>
        public static void LoadGameplay()
        {
            ServiceLocator.TryGetService<ILoadingScreenService>(out var loading);
            loading?.Show();
            CoroutineHost.Run(LoadGameplayAsync(loading));
        }

        /// <summary>
        /// Corre en <see cref="CoroutineHost"/> (no en una screen) porque las screens
        /// se destruyen con la escena vieja apenas <c>allowSceneActivation</c> dispara
        /// el swap.
        /// </summary>
        private static IEnumerator LoadGameplayAsync(ILoadingScreenService loading)
        {
            var op = SceneManager.LoadSceneAsync(GameplaySceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                loading?.ReportProgress(op.progress / 0.9f);
                yield return null;
            }

            op.allowSceneActivation = true;
            yield return op;

            loading?.ReportProgress(1f);
        }
    }
}
