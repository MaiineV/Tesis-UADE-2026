using Patterns;
using Rollgeon.Combat.Handoff;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Input
{
    /// <summary>
    /// Router global del click derecho en combate (QoL): cancela la selección de
    /// acción en curso (targeting de chain / tile de Movement) o, si no hay ninguna,
    /// deselecciona todos los dados holdeados (Balatro-style). La decisión vive en
    /// <see cref="RightClickCancelPolicy"/>; la prioridad selección-vs-dados en
    /// <see cref="ICombatHandoffService.HasCancellableSelection"/>.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en un GameObject siempre-activo de <c>02_Gameplay</c> (el mismo
    /// host que <see cref="PauseHotkey"/>), por las mismas razones de lifecycle.
    /// Polea en <c>LateUpdate</c> a propósito: los throw presenters manejan su
    /// right-click (cancel de agarre) en <c>Update</c> y claimean el frame vía
    /// <see cref="RightClickClaim"/> — así el orden queda garantizado sin depender
    /// de Script Execution Order.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Input/Right Click Cancel Controller")]
    public sealed class RightClickCancelController : MonoBehaviour
    {
        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;

            ServiceLocator.TryGetService<IScreenManager>(out var screens);
            var hud = screens?.Current as CombatHUDView;
            ServiceLocator.TryGetService<ICombatHandoffService>(out var handoff);

            var action = RightClickCancelPolicy.Decide(
                combatHudActive: hud != null,
                claimedByDiceGrab: RightClickClaim.WasClaimedThisFrame,
                hasCancellableSelection: handoff != null && handoff.HasCancellableSelection,
                anyDieSelected: hud != null && hud.AnyDieHeld(),
                uiSequencePending: Rollgeon.Feedback.BreakdownUiGate.Pending
                                   || Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.OutroPending);

            switch (action)
            {
                case RightClickAction.CancelSelection:
                    handoff.TryCancelFromRightClick();
                    break;
                case RightClickAction.DeselectAllDice:
                    hud.ClearDiceHolds();
                    break;
            }
        }
    }
}
