using Patterns;
using Rollgeon.Exploration;
using Rollgeon.Phase;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Input
{
    /// <summary>
    /// Hotkey <c>X</c> (action <c>Gameplay/CancelMove</c>): cancela la caminata
    /// click-to-move en curso en exploración — el pawn frena al completar el step
    /// actual (ver <see cref="IExplorationBehaviorService.TryCancelPendingWalk"/>).
    /// Fuera de la fase de exploración es no-op; en el tutorial con el map
    /// suprimido queda inerte gratis.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en el mismo GameObject siempre-activo de <c>02_Gameplay</c> que
    /// <see cref="PauseHotkey"/> y <see cref="RightClickCancelController"/>.
    /// Suscribe en <c>Start</c>: <c>GameplayHotkeyService</c> registra su servicio
    /// en <c>Awake</c>, así que el Start de un sibling llega siempre después.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Input/Exploration Move Cancel Controller")]
    public sealed class ExplorationMoveCancelController : MonoBehaviour
    {
        private IGameplayHotkeyService _hotkeys;

        private void Start()
        {
            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out var hotkeys) && hotkeys != null)
            {
                _hotkeys = hotkeys;
                _hotkeys.Subscribe(GameplayHotkey.CancelMove, OnCancelMove);
            }
        }

        private void OnDestroy()
        {
            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.CancelMove, OnCancelMove);
                _hotkeys = null;
            }
        }

        private void OnCancelMove(InputAction.CallbackContext _)
        {
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase)
                || phase == null || phase.CurrentBase != Rollgeon.Phase.GamePhase.Exploration)
                return;

            if (ServiceLocator.TryGetService<IExplorationBehaviorService>(out var exploration)
                && exploration != null)
            {
                exploration.TryCancelPendingWalk();
            }
        }
    }
}
