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

        [Title("Action Colors")]
        [InfoBox("Color de fondo de cada botón según el tipo de acción. Se aplica al Image " +
                 "del botón en Awake. Los desactivados se atenúan automáticamente vía Button.colors.")]
        [SerializeField]
        private Color _movementColor = new Color(0.27f, 0.55f, 0.95f, 1f);   // azul

        [SerializeField]
        private Color _attackColor = new Color(0.93f, 0.30f, 0.30f, 1f);     // rojo

        [SerializeField]
        private Color _specialColor = new Color(0.69f, 0.34f, 0.93f, 1f);    // violeta

        [SerializeField]
        private Color _healColor = new Color(0.32f, 0.82f, 0.45f, 1f);       // verde

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

        // Tracking de acciones usadas en el turno actual. Resetea al disparar
        // OnTurnStarted (jugador) — no usamos TurnManager.WasUsedThisTurn directo
        // porque indexa por ActionName y queremos clave por slot 0-3.
        private readonly bool[] _usedInTurn = new bool[4];

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

            ApplyActionColors();
        }

        /// <summary>
        /// Pinta el <see cref="UnityEngine.UI.Image.color"/> de cada botón con el color
        /// configurado en Inspector. Lo hace en Awake para no requerir setup manual de
        /// colores en el prefab.
        /// </summary>
        private void ApplyActionColors()
        {
            ApplyColor(_movementButton, _movementColor);
            ApplyColor(_attackButton, _attackColor);
            ApplyColor(_specialButton, _specialColor);
            ApplyColor(_healButton, _healColor);
        }

        private static void ApplyColor(Button button, Color color)
        {
            if (button == null) return;
            if (button.targetGraphic is UnityEngine.UI.Image img)
            {
                img.color = color;
            }
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
            EventManager.Subscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
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
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            _bound = false;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        private void HandleInventoryChanged(params object[] args)
        {
            // Recalcula availability — el Heal queda gris si te quedaste sin pociones.
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

            // Cada botón se habilita sólo si la fase lo permite, no se usó en este turno
            // Y la behavior puede ejecutar (preconditions OK — ej. heal con poción disponible).
            if (_movementButton != null)
                _movementButton.interactable = behaviors && !_usedInTurn[0] && IsBehaviorAvailable(HeroBehaviorSlot.Movement);
            if (_attackButton != null)
                _attackButton.interactable = behaviors && !_usedInTurn[1] && IsBehaviorAvailable(HeroBehaviorSlot.BaseAttack);
            if (_specialButton != null)
                _specialButton.interactable = behaviors && !_usedInTurn[2] && IsBehaviorAvailable(HeroBehaviorSlot.SpecialAttack);
            if (_healButton != null)
                _healButton.interactable = behaviors && !_usedInTurn[3] && IsBehaviorAvailable(HeroBehaviorSlot.Healing);

            if (_confirmButton != null) _confirmButton.interactable = confirm;
        }

        private bool IsBehaviorAvailable(HeroBehaviorSlot slot)
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps?.CurrentHero == null) return true;
            var behavior = ps.CurrentHero.ResolveBaseBehavior(slot, GamePhase.Combat);
            if (behavior == null) return false;
            return behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _);
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            // Turno nuevo del jugador: resetear flags de uso. TurnManager hace lo
            // mismo internamente con _actionsUsedThisTurn.
            for (int i = 0; i < _usedInTurn.Length; i++) _usedInTurn[i] = false;
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

            // Marcamos el slot como usado en este turno y refrescamos. La cancelación
            // del effect chain (ej. precondición falla) no rollbackea visualmente —
            // para FP lo aceptamos: si el usuario clickea un botón válido, asumimos
            // que va a ejecutarse o dará feedback explícito de error.
            if (index >= 0 && index < _usedInTurn.Length)
            {
                _usedInTurn[index] = true;
                RefreshInteractable();
            }
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
