using Patterns;
using Rollgeon.Combat.Handoff;
using Rollgeon.Exploration;
using Rollgeon.Phase;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Input
{
    /// <summary>
    /// Hotkey <c>X</c> (action <c>Gameplay/CancelMove</c>): en exploración cancela
    /// la caminata click-to-move en curso — el pawn frena al completar el step
    /// actual (ver <see cref="IExplorationBehaviorService.TryCancelPendingWalk"/>).
    /// En combate cancela el Movement esperando su tile destino (ver
    /// <see cref="ICombatHandoffService.TryCancelMovementSelection"/>) — mismo
    /// gesto en ambos modos; el click derecho quedó solo para chain/dados.
    /// En cualquier otra fase es no-op; en el tutorial con el map suprimido queda
    /// inerte gratis.
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
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase) || phase == null)
                return;

            switch (phase.CurrentBase)
            {
                case GamePhase.Exploration:
                    if (ServiceLocator.TryGetService<IExplorationBehaviorService>(out var exploration)
                        && exploration != null)
                    {
                        exploration.TryCancelPendingWalk();
                    }
                    break;

                case GamePhase.Combat:
                    if (ServiceLocator.TryGetService<ICombatHandoffService>(out var handoff)
                        && handoff != null)
                    {
                        handoff.TryCancelMovementSelection();
                    }
                    break;
            }
        }
    }
}
