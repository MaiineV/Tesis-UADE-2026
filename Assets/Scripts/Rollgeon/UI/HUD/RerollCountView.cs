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
    /// Sub-view que muestra "{used}/{cap}" rerolls + un boton "extra roll (1E)".
    /// Consume <see cref="IRerollBudgetService"/> via <see cref="Patterns.ServiceLocator"/>
    /// y escucha <see cref="EventName.OnDiceRolled"/> / <see cref="EventName.OnRollResolved"/>
    /// + el evento tipado <see cref="IRerollBudgetService.OnRerollStarted"/>.
    /// Plan §3.7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fallback</b>: si el servicio no esta registrado al Bind, el label muestra
    /// <c>"-/-"</c> y el boton queda deshabilitado.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Reroll Count View")]
    public class RerollCountView : MonoBehaviour
    {
        private const string LogPrefix = "[RerollCountView] ";

        [Title("Reroll Count — Widgets")]
        [SerializeField]
        [Tooltip("Label '{used}/{cap}'. Fallback '-/-' si no hay IRerollBudgetService.")]
        private TextMeshProUGUI _countLabel;

        [SerializeField]
        [Tooltip("Boton 'extra roll (1E)'. Mirrea ActionButtonsView._energyRerollButton " +
                 "pero es una afordance separada pegada a la dice zone.")]
        private Button _extraRollButton;

        [Title("Reroll Count — Config")]
        [SerializeField]
        [Tooltip("Formato del label. Default '{0}/{1}'.")]
        private string _countFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Texto fallback cuando no hay IRerollBudgetService.")]
        private string _fallbackText = "-/-";

        [SerializeField]
        [Tooltip("Label opcional de costo del proximo reroll (ej. 'Free', '1E'). Null = skip.")]
        private TextMeshProUGUI _costLabel;

        [Title("Reroll Count — Events")]
        [SerializeField]
        private UnityEvent _onExtraRollPressed = new UnityEvent();

        public UnityEvent OnExtraRollPressed => _onExtraRollPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private IRerollBudgetService _budget;
        private Action<RerollStartedPayload> _onRerollStartedTyped;
        private Rollgeon.ActionRolls.IActionRollService _actionRoll;
        private Action<Rollgeon.ActionRolls.ActionRollPhase> _onActionRollPhase;

        private void Awake()
        {
            if (_extraRollButton != null) _extraRollButton.onClick.AddListener(HandleExtraRollClick);
        }

        private void OnDestroy()
        {
            if (_extraRollButton != null) _extraRollButton.onClick.RemoveListener(HandleExtraRollClick);
        }

        public void Bind(Guid playerGuid)
        {
            // Idempotente para soporte multi-HUD (CombatHUD + ExplorationHUD ambos bindean
            // ahora que vive en el Canvas raíz). Skip si ya estoy bindeado al mismo guid.
            if (_bound)
            {
                if (_playerGuid == playerGuid) return;
                Unbind();
            }
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out _budget) && _budget != null)
            {
                _onRerollStartedTyped = HandleRerollStartedTyped;
                _budget.OnRerollStarted += _onRerollStartedTyped;
            }
            else
            {
                Debug.Log(LogPrefix + "IRerollBudgetService no registrado — label en fallback.", this);
                _budget = null;
            }

            // Suscripción al ActionRollService: OnDiceRolled se dispara mientras la phase
            // todavía es Rolling — necesitamos refrescar también cuando entra a
            // AwaitingRerollDecision para que el botón se habilite si hay energía.
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out _actionRoll)
                && _actionRoll != null)
            {
                _onActionRollPhase = _ => RefreshButtonInteractable();
                _actionRoll.OnPhaseChanged += _onActionRollPhase;
            }

            _bound = true;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);

            if (_budget != null && _onRerollStartedTyped != null)
            {
                _budget.OnRerollStarted -= _onRerollStartedTyped;
                _onRerollStartedTyped = null;
            }
            if (_actionRoll != null && _onActionRollPhase != null)
            {
                _actionRoll.OnPhaseChanged -= _onActionRollPhase;
                _onActionRollPhase = null;
                _actionRoll = null;
            }
            _budget = null;
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica
        // ======================================================================

        /// <summary>Pinta el contador "{used}/{cap}" manualmente. Publico para tooling / tests.</summary>
        public void SetCount(int used, int cap)
        {
            if (_countLabel == null) return;
            _countLabel.text = string.Format(_countFormat, used, cap);
        }

        /// <summary>Pinta el label en fallback (servicio ausente).</summary>
        public void SetFallback()
        {
            if (_countLabel == null) return;
            _countLabel.text = _fallbackText;
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleExtraRollClick()
        {
            // Si hay un ActionRoll activo (Heal / Forzar Puerta), Reroll = pagar 1 energía
            // y rerollear via service. El service usa _currentHolds (seteado por
            // DiceZoneView.ToggleHold → SetHolds) como keep mask.
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                rs.RequestReroll();
                return;
            }
            _onExtraRollPressed?.Invoke();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            if (_budget == null)
                ServiceLocator.TryGetService<IRerollBudgetService>(out _budget);
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            SetFallback();
            RefreshCostLabel();
            if (_extraRollButton != null) _extraRollButton.interactable = false;
        }

        private void HandleRerollStartedTyped(RerollStartedPayload payload)
        {
            if (payload.PlayerGuid != _playerGuid) return;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void RefreshLabel()
        {
            if (_budget == null || _budget.Current == null)
            {
                SetFallback();
                return;
            }
            var action = _budget.Current.Action;
            int initialFree = action != null ? Math.Max(0, action.FreeRollCount - 1) : 0;
            int freeConsumed = initialFree - _budget.Current.FreeRollsRemaining;
            if (freeConsumed < 0) freeConsumed = 0;
            int consumed = freeConsumed + _budget.Current.PaidRollsUsed;
            SetCount(consumed, initialFree);
        }

        private void RefreshCostLabel()
        {
            if (_costLabel == null) return;
            if (_budget == null)
            {
                _costLabel.text = "";
                return;
            }
            var query = _budget.QueryExtraRoll(_playerGuid);
            if (query.IsFreeRoll)
                _costLabel.text = "Free";
            else if (query.CostsEnergy)
                _costLabel.text = "1E";
            else
                _costLabel.text = "";
        }

        private void RefreshButtonInteractable()
        {
            if (_extraRollButton == null) return;

            // Si hay un ActionRoll activo (Heal / Forzar Puerta), el budget de Generala
            // no aplica — el gating es por energía vía CanAffordReroll del service.
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                _extraRollButton.interactable = rs.CanAffordReroll;
                return;
            }

            if (_budget == null)
            {
                _extraRollButton.interactable = false;
                return;
            }

            var query = _budget.QueryExtraRoll(_playerGuid);
            _extraRollButton.interactable = query.IsAvailable;
        }
    }
}
