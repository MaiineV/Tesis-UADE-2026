using System;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/End Turn Button View")]
    public class EndTurnButtonView : MonoBehaviour
    {
        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

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
    }
}
