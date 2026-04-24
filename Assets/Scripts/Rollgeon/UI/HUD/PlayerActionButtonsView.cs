using System;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Player Action Buttons View")]
    public class PlayerActionButtonsView : MonoBehaviour
    {
        private const string LogPrefix = "[PlayerActionButtonsView] ";

        // ======================================================================
        // Serialized fields — behavior buttons
        // ======================================================================

        [Title("Behavior Buttons")]
        [SerializeField]
        private Button _movementButton;

        [SerializeField]
        private Button _attackButton;

        [SerializeField]
        private Button _specialButton;

        [SerializeField]
        private Button _healButton;

        // ======================================================================
        // Serialized fields — action buttons
        // ======================================================================

        [Title("Action Buttons")]
        [Required("Arrastrar el boton de Reroll.")]
        [SerializeField]
        private Button _rerollButton;

        [Required("Arrastrar el boton de Confirm.")]
        [SerializeField]
        private Button _confirmButton;

        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

        [Required("Arrastrar el label de reroll info.")]
        [SerializeField]
        private TextMeshProUGUI _rerollLabel;

        // ======================================================================
        // Serialized fields — legacy (backward compat)
        // ======================================================================

        [Title("Legacy (deprecated)")]
        [SerializeField]
        private Button _rollDiceButton;

        [SerializeField]
        private Button _confirmAttackButton;

        // ======================================================================
        // Events
        // ======================================================================

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onRerollPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onConfirmPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onEndTurnPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onRollDicePressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onConfirmAttackPressed = new UnityEvent();

        public UnityEvent OnRerollPressed => _onRerollPressed;
        public UnityEvent OnConfirmPressed => _onConfirmPressed;
        public UnityEvent OnEndTurnPressed => _onEndTurnPressed;

        public UnityEvent OnRollDicePressed => _onRollDicePressed;
        public UnityEvent OnConfirmAttackPressed => _onConfirmAttackPressed;

        public Action<int> OnBehaviorSelected;

        // ======================================================================
        // Internal state
        // ======================================================================

        public enum ButtonPhase { Idle, WaitingForAction, Rolled }

        [ShowInInspector, ReadOnly]
        private ButtonPhase _phase = ButtonPhase.Idle;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        [ShowInInspector, ReadOnly]
        private bool _selectedBehaviorAllowsReroll;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_movementButton != null) _movementButton.onClick.AddListener(() => HandleBehaviorClick(0));
            if (_attackButton != null) _attackButton.onClick.AddListener(() => HandleBehaviorClick(1));
            if (_specialButton != null) _specialButton.onClick.AddListener(() => HandleBehaviorClick(2));
            if (_healButton != null) _healButton.onClick.AddListener(() => HandleBehaviorClick(3));

            if (_rerollButton != null) _rerollButton.onClick.AddListener(HandleRerollClick);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmClick);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurnClick);

            if (_rollDiceButton != null) _rollDiceButton.onClick.AddListener(HandleRollDiceClick);
            if (_confirmAttackButton != null) _confirmAttackButton.onClick.AddListener(HandleConfirmAttackClick);
        }

        private void OnDestroy()
        {
            if (_movementButton != null) _movementButton.onClick.RemoveAllListeners();
            if (_attackButton != null) _attackButton.onClick.RemoveAllListeners();
            if (_specialButton != null) _specialButton.onClick.RemoveAllListeners();
            if (_healButton != null) _healButton.onClick.RemoveAllListeners();

            if (_rerollButton != null) _rerollButton.onClick.RemoveListener(HandleRerollClick);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(HandleConfirmClick);
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleEndTurnClick);

            if (_rollDiceButton != null) _rollDiceButton.onClick.RemoveListener(HandleRollDiceClick);
            if (_confirmAttackButton != null) _confirmAttackButton.onClick.RemoveListener(HandleConfirmAttackClick);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // Public API
        // ======================================================================

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnEnergyChanged, HandleEnergyChanged);
            EventManager.Subscribe(EventName.OnRerollBudgetChanged, HandleRerollBudgetChanged);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            _bound = true;

            if (ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder)
                && turnOrder.ParticipantCount > 0
                && turnOrder.Current == _playerGuid)
            {
                _phase = ButtonPhase.WaitingForAction;
            }
            else
            {
                _phase = ButtonPhase.Idle;
            }
            RefreshInteractable();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnEnergyChanged, HandleEnergyChanged);
            EventManager.UnSubscribe(EventName.OnRerollBudgetChanged, HandleRerollBudgetChanged);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            _bound = false;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        public void NotifyBehaviorAllowsReroll(bool allowsReroll)
        {
            _selectedBehaviorAllowsReroll = allowsReroll;
        }

        public void RefreshInteractable()
        {
            bool behaviors = false;
            bool reroll = false;
            bool confirm = false;
            bool endTurn = false;

            switch (_phase)
            {
                case ButtonPhase.Idle:
                    break;
                case ButtonPhase.WaitingForAction:
                    behaviors = true;
                    endTurn = true;
                    break;
                case ButtonPhase.Rolled:
                    reroll = _selectedBehaviorAllowsReroll && CanReroll();
                    confirm = true;
                    endTurn = false;
                    break;
            }

            if (_movementButton != null) _movementButton.interactable = behaviors;
            if (_attackButton != null) _attackButton.interactable = behaviors;
            if (_specialButton != null) _specialButton.interactable = behaviors;
            if (_healButton != null) _healButton.interactable = behaviors;

            if (_rerollButton != null) _rerollButton.interactable = reroll;
            if (_confirmButton != null) _confirmButton.interactable = confirm;
            if (_endTurnButton != null) _endTurnButton.interactable = endTurn;

            if (_rollDiceButton != null) _rollDiceButton.interactable = behaviors;
            if (_confirmAttackButton != null) _confirmAttackButton.interactable = confirm;

            UpdateRerollLabel();
        }

        private void UpdateRerollLabel()
        {
            if (_rerollLabel == null) return;

            if (!ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) || budget == null)
            {
                _rerollLabel.text = "Reroll";
                return;
            }

            var query = budget.QueryExtraRoll(_playerGuid);
            if (query.IsFreeRoll)
                _rerollLabel.text = "Reroll (Free)";
            else if (query.CostsEnergy)
                _rerollLabel.text = "Reroll (1E)";
            else
                _rerollLabel.text = query.BlockedReason ?? "Reroll";
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.WaitingForAction;
            _selectedBehaviorAllowsReroll = false;
            RefreshInteractable();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.Rolled;
            RefreshInteractable();
        }

        private void HandleEnergyChanged(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshInteractable();
        }

        private void HandleRerollBudgetChanged(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshInteractable();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.WaitingForAction;
            _selectedBehaviorAllowsReroll = false;
            RefreshInteractable();
        }

        // ======================================================================
        // Click handlers
        // ======================================================================

        private void HandleBehaviorClick(int index)
        {
            OnBehaviorSelected?.Invoke(index);
        }

        private void HandleRerollClick()
        {
            _onRerollPressed?.Invoke();
        }

        private void HandleConfirmClick()
        {
            _onConfirmPressed?.Invoke();
        }

        private void HandleEndTurnClick()
        {
            _onEndTurnPressed?.Invoke();
        }

        private void HandleRollDiceClick()
        {
            _onRollDicePressed?.Invoke();
        }

        private void HandleConfirmAttackClick()
        {
            _onConfirmAttackPressed?.Invoke();
        }

        // ======================================================================
        // Gating helpers
        // ======================================================================

        private bool CanReroll()
        {
            if (!ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) || budget == null)
                return false;
            return budget.QueryExtraRoll(_playerGuid).IsAvailable;
        }
    }
}
