using System;
using Patterns;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view con 4 botones que siguen el flujo dice-first del combate:
    /// Roll Dice -> Reroll / Confirm -> End Turn.
    /// El estado de cada boton depende de la <see cref="ButtonPhase"/> actual,
    /// derivada de eventos del bus (<c>OnTurnStarted</c>, <c>OnDiceRolled</c>,
    /// <c>OnRollResolved</c>, <c>OnTurnFinished</c>).
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Player Action Buttons View")]
    public class PlayerActionButtonsView : MonoBehaviour
    {
        private const string LogPrefix = "[PlayerActionButtonsView] ";

        // ======================================================================
        // Serialized fields
        // ======================================================================

        [Title("Player Action Buttons - Widgets")]
        [Required("Arrastrar el boton de Roll Dice.")]
        [SerializeField]
        private Button _rollDiceButton;

        [Required("Arrastrar el boton de Reroll.")]
        [SerializeField]
        private Button _rerollButton;

        [Required("Arrastrar el boton de Confirm Attack.")]
        [SerializeField]
        private Button _confirmAttackButton;

        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

        [Required("Arrastrar el label de reroll info.")]
        [SerializeField]
        private TextMeshProUGUI _rerollLabel;

        [Title("Player Action Buttons - Events")]
        [SerializeField]
        private UnityEvent _onRollDicePressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onRerollPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onConfirmAttackPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onEndTurnPressed = new UnityEvent();

        public UnityEvent OnRollDicePressed => _onRollDicePressed;
        public UnityEvent OnRerollPressed => _onRerollPressed;
        public UnityEvent OnConfirmAttackPressed => _onConfirmAttackPressed;
        public UnityEvent OnEndTurnPressed => _onEndTurnPressed;

        // ======================================================================
        // Internal state
        // ======================================================================

        private enum ButtonPhase { Idle, WaitingForRoll, Rolled, Resolved }

        [ShowInInspector, ReadOnly]
        private ButtonPhase _phase = ButtonPhase.Idle;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_rollDiceButton != null) _rollDiceButton.onClick.AddListener(HandleRollDiceClick);
            if (_rerollButton != null) _rerollButton.onClick.AddListener(HandleRerollClick);
            if (_confirmAttackButton != null) _confirmAttackButton.onClick.AddListener(HandleConfirmAttackClick);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurnClick);
        }

        private void OnDestroy()
        {
            if (_rollDiceButton != null) _rollDiceButton.onClick.RemoveListener(HandleRollDiceClick);
            if (_rerollButton != null) _rerollButton.onClick.RemoveListener(HandleRerollClick);
            if (_confirmAttackButton != null) _confirmAttackButton.onClick.RemoveListener(HandleConfirmAttackClick);
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleEndTurnClick);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // Public API
        // ======================================================================

        /// <summary>Suscribe al bus. Arranca en Idle con todos los botones deshabilitados.</summary>
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

            _phase = ButtonPhase.Idle;
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
        }

        /// <summary>Reads _phase + service queries, updates all button interactable states.</summary>
        public void RefreshInteractable()
        {
            bool rollDice = false;
            bool reroll = false;
            bool confirm = false;
            bool endTurn = false;

            switch (_phase)
            {
                case ButtonPhase.Idle:
                    break;
                case ButtonPhase.WaitingForRoll:
                    rollDice = true;
                    endTurn = true;
                    break;
                case ButtonPhase.Rolled:
                    reroll = CanReroll();
                    confirm = true;
                    endTurn = true;
                    break;
                case ButtonPhase.Resolved:
                    break;
            }

            if (_rollDiceButton != null) _rollDiceButton.interactable = rollDice;
            if (_rerollButton != null) _rerollButton.interactable = reroll;
            if (_confirmAttackButton != null) _confirmAttackButton.interactable = confirm;
            if (_endTurnButton != null) _endTurnButton.interactable = endTurn;

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
            _phase = ButtonPhase.WaitingForRoll;
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
            _phase = ButtonPhase.Resolved;
            RefreshInteractable();
        }

        // ======================================================================
        // Click handlers
        // ======================================================================

        private void HandleRollDiceClick()
        {
            _onRollDicePressed?.Invoke();
        }

        private void HandleRerollClick()
        {
            _onRerollPressed?.Invoke();
        }

        private void HandleConfirmAttackClick()
        {
            _onConfirmAttackPressed?.Invoke();
        }

        private void HandleEndTurnClick()
        {
            _onEndTurnPressed?.Invoke();
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
