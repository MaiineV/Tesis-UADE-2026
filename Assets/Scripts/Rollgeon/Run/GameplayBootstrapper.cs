using Patterns;
using Rollgeon.Balance;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// MonoBehaviour escena-scoped para <c>02_Gameplay</c>. Lee
    /// <see cref="PendingRunRequest"/>, arranca la run via
    /// <see cref="RunBootstrapper.StartRun"/> (que crea la scope Run + wirea
    /// servicios via <c>IRunController</c>), y pushea <c>ExplorationHUD</c>.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject vive en 02_Gameplay.unity. Sin fields serializados.
    /// Execution order -500 para correr despues de ScreenHost (-1000) y antes
    /// de gameplay MonoBehaviours default (0).
    /// </remarks>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("Rollgeon/Run/Gameplay Bootstrapper")]
    public sealed class GameplayBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "[GameplayBootstrapper] ";

        private void Start()
        {
            if (!PendingRunRequest.HasRequest)
            {
                Debug.LogError(LogPrefix + "No pending run request. Cargaste 02_Gameplay sin pasar por BuildSelection?", this);
                return;
            }

            var hero = PendingRunRequest.SelectedHero;
            var runId = PendingRunRequest.RunId;

            ServiceLocator.TryGetService<RulesetSO>(out var ruleset);

            RunBootstrapper.StartRun(hero, ruleset, runId);
            Debug.Log(LogPrefix + $"Run started. hero={hero.EntityId}, runId={runId}", this);

            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("ExplorationHUD");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — el ScreenHost de 02_Gameplay no corrio todavia?", this);
            }

            PendingRunRequest.Clear();
        }
    }
}
