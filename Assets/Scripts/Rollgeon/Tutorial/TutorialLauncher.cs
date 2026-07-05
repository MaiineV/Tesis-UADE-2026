using System;
using System.Collections;
using Patterns;
using Rollgeon.Run;
using Rollgeon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoroutineHost = Rollgeon.Patterns.CoroutineHost;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Punto de entrada al Tutorial Mode desde el main menu. Setea
    /// <see cref="PendingRunRequest"/> con el héroe fijo del tutorial
    /// (<c>IsTutorial=true</c>, sin class/build selection) y carga
    /// <c>02_Gameplay</c> con el mismo patrón async del
    /// <c>BuildSelectionScreen</c>. Downstream, <c>RunController.OnRunStart</c>
    /// detecta el flag y genera el piso fijo del tutorial.
    /// </summary>
    public static class TutorialLauncher
    {
        private const string LogPrefix = "[TutorialLauncher] ";

        /// <summary>
        /// <c>true</c> si hay un <see cref="TutorialConfigSO"/> registrado y completo —
        /// sin él, el caller debe caer al flujo normal.
        /// </summary>
        public static bool CanLaunch()
        {
            return ServiceLocator.TryGetService<TutorialConfigSO>(out var config)
                   && config != null && config.IsLaunchable;
        }

        /// <returns><c>false</c> si no hay config lanzable — el caller decide el fallback.</returns>
        public static bool Launch()
        {
            if (!ServiceLocator.TryGetService<TutorialConfigSO>(out var config)
                || config == null || !config.IsLaunchable)
            {
                Debug.LogWarning(LogPrefix + "TutorialConfigSO no registrado o incompleto — no se puede lanzar el tutorial.");
                return false;
            }

            // builtDiceBag null → RunBootstrapper cae al StartingDiceBagRef del héroe
            // (bolsa default fija, coherente con la experiencia controlada del tutorial).
            PendingRunRequest.Set(
                config.TutorialHero,
                Guid.NewGuid(),
                config.RulesetId,
                builtDiceBag: null,
                startingItems: null,
                isTutorial: true);

            ServiceLocator.TryGetService<ILoadingScreenService>(out var loading);
            loading?.Show();
            CoroutineHost.Run(LoadGameplayAsync(loading));

            Debug.Log(LogPrefix + $"Lanzando tutorial. hero={config.TutorialHero.EntityId}");
            return true;
        }

        // Mismo patrón que BuildSelectionScreen.LoadGameplayAsync: corre en
        // CoroutineHost porque la screen que disparó el launch muere con la escena.
        private static IEnumerator LoadGameplayAsync(ILoadingScreenService loading)
        {
            var op = SceneManager.LoadSceneAsync("02_Gameplay");
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
