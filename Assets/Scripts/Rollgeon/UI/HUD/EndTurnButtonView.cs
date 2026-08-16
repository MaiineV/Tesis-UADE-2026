using System;
using Patterns;
using Rollgeon.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/End Turn Button View")]
    public class EndTurnButtonView : MonoBehaviour
    {
        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

        [Tooltip("Opcional — highlight de 'sin energía' (glow + dots por el contorno). " +
                 "Si null, el botón no reacciona a la energía.")]
        [SerializeField]
        private EndTurnEnergyHighlight _energyHighlight;

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onEndTurnPressed = new UnityEvent();

        public UnityEvent OnEndTurnPressed => _onEndTurnPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        [ShowInInspector, ReadOnly]
        private bool _enabled;

        private IGameplayHotkeyService _hotkeys;

        /// <summary>RectTransform del botón End Turn — anchor del overlay del tutorial.</summary>
        public bool TryGetButtonRect(out RectTransform rect)
        {
            rect = _endTurnButton != null ? _endTurnButton.transform as RectTransform : null;
            return rect != null;
        }

        private void Awake()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleClick);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);

            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out _hotkeys) && _hotkeys != null)
                _hotkeys.Subscribe(GameplayHotkey.EndTurn, OnHotkeyEndTurn);

            if (_energyHighlight != null) _energyHighlight.Bind(playerGuid);

            _bound = true;
            _enabled = false;
            RefreshInteractable();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);

            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.EndTurn, OnHotkeyEndTurn);
                _hotkeys = null;
            }

            if (_energyHighlight != null) _energyHighlight.Unbind();

            _bound = false;
            _enabled = false;
            RefreshInteractable();
        }

        public void RefreshInteractable()
        {
            if (_endTurnButton != null) _endTurnButton.interactable = _enabled;
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _enabled = true;
            RefreshInteractable();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _enabled = false;
            RefreshInteractable();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _enabled = false;
            RefreshInteractable();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _enabled = true;
            RefreshInteractable();
        }

        private void HandleClick()
        {
            _onEndTurnPressed?.Invoke();
        }

        // Space = click de End Turn, solo si el botón está interactable (mismo gating).
        private void OnHotkeyEndTurn(InputAction.CallbackContext _)
        {
            // Space también confirma el roll (Confirm). Si Confirm ya consumió este press,
            // no pasamos turno: confirmar resuelve el roll y re-habilita este botón en el
            // mismo frame, y sin este guard el press pasaría turno de más.
            if (_hotkeys != null && _hotkeys.WasFrameConsumed()) return;
            if (_endTurnButton != null && _endTurnButton.interactable)
                _endTurnButton.onClick.Invoke();
        }
    }
}
