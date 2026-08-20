using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Dueño del botón "Confirm" de la zona de dados cuando hay un
    /// <see cref="IActionRollService"/> activo: gatea el <see cref="Button"/> con
    /// <c>CanConfirm</c>, resuelve la tirada al click (<c>DeclineReroll</c>) y
    /// atiende el hotkey Confirm (Space). En combate este botón queda oculto
    /// (<see cref="ActionRollExplorationVisibility"/> exploración-only) — ahí el
    /// confirm vive en el botón contextual de turno (<c>EndTurnButtonView</c>).
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Roll Confirm Gate")]
    [RequireComponent(typeof(Button))]
    public sealed class ActionRollConfirmGate : MonoBehaviour
    {
        private Button _button;
        private CanvasGroup _group;
        private IActionRollService _service;
        private System.Action<ActionRollPhase> _onPhase;
        private IGameplayHotkeyService _hotkeys;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _group = GetComponent<CanvasGroup>();
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private void OnEnable()
        {
            TrySubscribeToServices();
            Refresh();
        }

        private void OnDisable()
        {
            if (_service != null && _onPhase != null)
            {
                _service.OnPhaseChanged -= _onPhase;
                _onPhase = null;
                _service = null;
            }
            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.Confirm, OnHotkeyConfirm);
                _hotkeys = null;
            }
        }

        private void Update()
        {
            // Los services son Run-scoped y pueden registrarse después del OnEnable
            // de la escena — retry barato hasta engancharlos (mismo patrón que
            // ActionRollExplorationVisibility).
            TrySubscribeToServices();

            // CanConfirm cambia con los holds (DiceZoneView.ToggleHold → service.SetHolds),
            // pero el service no emite un evento "holds changed". Polling barato en Update
            // — solo cuando hay action roll activa. Si no es activa, no tocamos el button.
            if (_service != null && _service.IsActive) Refresh();
        }

        private void TrySubscribeToServices()
        {
            if (_service == null
                && ServiceLocator.TryGetService(out _service) && _service != null)
            {
                _onPhase = _ => Refresh();
                _service.OnPhaseChanged += _onPhase;
            }

            if (_hotkeys == null
                && ServiceLocator.TryGetService(out _hotkeys) && _hotkeys != null)
            {
                _hotkeys.Subscribe(GameplayHotkey.Confirm, OnHotkeyConfirm);
            }
        }

        private void Refresh()
        {
            if (_button == null || _service == null) return;
            if (!_service.IsActive) return; // sin action roll, no toco el button.
            _button.interactable = _service.CanConfirm;
        }

        private void HandleClick()
        {
            // Resolver con la tirada actual — mismo path que tenía el viejo
            // HandleConfirmClick de PlayerActionButtonsView para ActionRolls.
            if (_service != null && _service.IsActive) _service.DeclineReroll();
        }

        // Space confirma solo si el botón está visible e interactable: en combate la
        // visibilidad exploración-only lo apaga (group.interactable=false) y el
        // confirm de Space lo maneja el botón contextual de turno.
        private void OnHotkeyConfirm(InputAction.CallbackContext _)
        {
            if (_button == null || !_button.interactable) return;
            if (_group != null && !_group.interactable) return;
            _button.onClick.Invoke();
        }
    }
}
