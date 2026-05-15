using Patterns;
using Rollgeon.ActionRolls;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Gating del <see cref="Button"/> "Confirm" cuando hay un <see cref="IActionRollService"/>
    /// activo: el botón solo se habilita si el service expone <c>CanConfirm</c> (hay holds
    /// seleccionados). Cuando NO hay action roll, este componente no toca el Button — el
    /// gating queda en manos del <c>PlayerActionButtonsView</c> (combat normal).
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Roll Confirm Gate")]
    [RequireComponent(typeof(Button))]
    public sealed class ActionRollConfirmGate : MonoBehaviour
    {
        private Button _button;
        private IActionRollService _service;
        private System.Action<ActionRollPhase> _onPhase;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGetService<IActionRollService>(out _service) && _service != null)
            {
                _onPhase = _ => Refresh();
                _service.OnPhaseChanged += _onPhase;
            }
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
        }

        private void Update()
        {
            // CanConfirm cambia con los holds (DiceZoneView.ToggleHold → service.SetHolds),
            // pero el service no emite un evento "holds changed". Polling barato en Update
            // — solo cuando hay action roll activa. Si no es activa, no tocamos el button.
            if (_service != null && _service.IsActive) Refresh();
        }

        private void Refresh()
        {
            if (_button == null || _service == null) return;
            if (!_service.IsActive) return; // sin action roll, no toco — el view de combat manda.
            _button.interactable = _service.CanConfirm;
        }
    }
}
