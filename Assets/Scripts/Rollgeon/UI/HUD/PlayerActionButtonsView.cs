using System;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
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

        [Title("Energy Cost Labels")]
        [InfoBox("Labels TMP opcionales que muestran cuánta energía consume cada acción. " +
                 "Si dejás uno null, el costo no se muestra en ese botón.")]
        [SerializeField]
        private TextMeshProUGUI _movementCostLabel;

        [SerializeField]
        private TextMeshProUGUI _attackCostLabel;

        [SerializeField]
        private TextMeshProUGUI _specialCostLabel;

        [SerializeField]
        private TextMeshProUGUI _healCostLabel;

        [SerializeField]
        [Tooltip("Formato del label de costo. Default '{0}'. Ej: '{0}E', '-{0}', '⚡{0}'.")]
        private string _costLabelFormat = "{0}";

        [SerializeField]
        [Tooltip("Texto mostrado cuando el costo es 0 (acción gratuita). Vacío oculta el label.")]
        private string _zeroCostText = "";

        // ======================================================================
        // Serialized fields — confirm button
        // ======================================================================

        [Title("Confirm")]
        [Required("Arrastrar el boton de Confirm.")]
        [SerializeField]
        private Button _confirmButton;

        // ======================================================================
        // Events
        // ======================================================================

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onConfirmPressed = new UnityEvent();

        public UnityEvent OnConfirmPressed => _onConfirmPressed;

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

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_movementButton != null) _movementButton.onClick.AddListener(() => HandleBehaviorClick(0));
            if (_attackButton != null) _attackButton.onClick.AddListener(() => HandleBehaviorClick(1));
            if (_specialButton != null) _specialButton.onClick.AddListener(() => HandleBehaviorClick(2));
            if (_healButton != null) _healButton.onClick.AddListener(() => HandleBehaviorClick(3));

            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmClick);
        }

        private void OnDestroy()
        {
            if (_movementButton != null) _movementButton.onClick.RemoveAllListeners();
            if (_attackButton != null) _attackButton.onClick.RemoveAllListeners();
            if (_specialButton != null) _specialButton.onClick.RemoveAllListeners();
            if (_healButton != null) _healButton.onClick.RemoveAllListeners();

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(HandleConfirmClick);
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
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            _bound = true;

            RefreshCostLabels();

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
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            _bound = false;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        public void RefreshInteractable()
        {
            bool behaviors = false;
            bool confirm = false;

            switch (_phase)
            {
                case ButtonPhase.Idle:
                    break;
                case ButtonPhase.WaitingForAction:
                    behaviors = true;
                    break;
                case ButtonPhase.Rolled:
                    confirm = true;
                    break;
            }

            if (_movementButton != null) _movementButton.interactable = behaviors;
            if (_attackButton != null) _attackButton.interactable = behaviors;
            if (_specialButton != null) _specialButton.interactable = behaviors;
            if (_healButton != null) _healButton.interactable = behaviors;

            if (_confirmButton != null) _confirmButton.interactable = confirm;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.WaitingForAction;
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

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.WaitingForAction;
            RefreshInteractable();
        }

        // ======================================================================
        // Click handlers
        // ======================================================================

        private void HandleBehaviorClick(int index)
        {
            OnBehaviorSelected?.Invoke(index);
        }

        private void HandleConfirmClick()
        {
            _onConfirmPressed?.Invoke();
        }

        // ======================================================================
        // Energy cost labels
        // ======================================================================

        /// <summary>
        /// Pinta cada label de costo con el <see cref="HeroActionBehavior.EnergyCost"/>
        /// del behavior que mapea a su slot. Llamado en <see cref="Bind"/>.
        /// </summary>
        public void RefreshCostLabels()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService?.CurrentHero == null)
            {
                ApplyCostText(_movementCostLabel, null);
                ApplyCostText(_attackCostLabel, null);
                ApplyCostText(_specialCostLabel, null);
                ApplyCostText(_healCostLabel, null);
                return;
            }

            var hero = playerService.CurrentHero;
            ApplyCostText(_movementCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.Movement, GamePhase.Combat));
            ApplyCostText(_attackCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.BaseAttack, GamePhase.Combat));
            ApplyCostText(_specialCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.SpecialAttack, GamePhase.Combat));
            ApplyCostText(_healCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.Healing, GamePhase.Combat));
        }

        private void ApplyCostText(TextMeshProUGUI label, HeroActionBehavior behavior)
        {
            if (label == null) return;
            if (behavior == null)
            {
                label.text = string.Empty;
                return;
            }

            label.text = behavior.EnergyCost <= 0
                ? _zeroCostText
                : string.Format(_costLabelFormat, behavior.EnergyCost);
        }
    }
}
