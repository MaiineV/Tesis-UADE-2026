using Patterns;
using Rollgeon.Balance;
using Rollgeon.Player;
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

            // 1. Push ExplorationHUD PRIMERO — queda en la base del stack.
            //    Cualquier overlay que pushee el chain de StartRun (CombatHUD,
            //    FloorTransition) aterriza correctamente encima.
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("ExplorationHUD");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — el ScreenHost de 02_Gameplay no corrio todavia?", this);
            }

            // 2. Ahora sí: arrancar la run. El chain
            //    (RunController.OnRunStart → ExplorationController.BeginExploration →
            //    ProcessRoom) puede pushear CombatHUD con seguridad.
            RunBootstrapper.StartRun(hero, ruleset, runId);
            Debug.Log(LogPrefix + $"Run started. hero={hero.EntityId}, runId={runId}", this);

            // 3. Bag construido en BuildSelectionScreen (Fase 2). Si vino, pisa lo
            //    que SetPlayer haya inferido del StartingDiceBagRef. Si no vino,
            //    el flujo cae al fallback de Fase 1 (StartingDiceBagRef o
            //    Resources/AD_Warrior_StartingBag) en CombatHandoffService.
            var builtBag = PendingRunRequest.BuiltDiceBag;
            if (builtBag != null && ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                playerService.SetDiceBag(builtBag);
                Debug.Log(LogPrefix + $"Aplicado built dice bag ({builtBag.Dice.Count} dados).", this);
            }

            PendingRunRequest.Clear();
        }
    }
}
