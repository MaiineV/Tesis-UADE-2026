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

        [SerializeField]
        [Tooltip("Label opcional del boton — cambia entre 'Roll', 'Reroll (Free)' y " +
                 "'Reroll (1E)' segun el estado del budget. Null = skip.")]
        private TextMeshProUGUI _buttonLabel;

        [Title("Reroll Count — Button Texts")]
        [SerializeField]
        [Tooltip("Texto del boton para el primer roll (antes de gastar ningun roll).")]
        private string _firstRollText = "Roll";

        [SerializeField]
        [Tooltip("Texto del boton para un reroll gratis.")]
        private string _rerollFreeText = "Reroll (Free)";

        [SerializeField]
        [Tooltip("Texto del boton para un reroll pago con energia.")]
        private string _rerollPaidText = "Reroll (1E)";

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
        private Action<RerollBudget> _onBudgetStartedTyped;

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
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out _budget) && _budget != null)
            {
                _onRerollStartedTyped = HandleRerollStartedTyped;
                _budget.OnRerollStarted += _onRerollStartedTyped;

                _onBudgetStartedTyped = HandleBudgetStartedTyped;
                _budget.OnBudgetStarted += _onBudgetStartedTyped;
            }
            else
            {
                Debug.Log(LogPrefix + "IRerollBudgetService no registrado — label en fallback.", this);
                _budget = null;
            }

            _bound = true;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
            RefreshButtonText();
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
            if (_budget != null && _onBudgetStartedTyped != null)
            {
                _budget.OnBudgetStarted -= _onBudgetStartedTyped;
                _onBudgetStartedTyped = null;
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
            RefreshButtonText();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            SetFallback();
            RefreshCostLabel();
            if (_buttonLabel != null) _buttonLabel.text = _firstRollText;
            if (_extraRollButton != null) _extraRollButton.interactable = false;
        }

        private void HandleRerollStartedTyped(RerollStartedPayload payload)
        {
            if (payload.PlayerGuid != _playerGuid) return;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
            RefreshButtonText();
        }

        private void HandleBudgetStartedTyped(RerollBudget budget)
        {
            // Repinta el contador apenas se abre el budget (al seleccionar accion),
            // sin esperar al primer OnDiceRolled. Hace que el "3/3" sea visible
            // desde la seleccion como pide el flow manual de roll.
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshCostLabel();
            RefreshButtonText();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void RefreshLabel()
        {
            if (_budget == null || _budget.Current == null || _budget.Current.Action == null)
            {
                SetFallback();
                return;
            }
            int total = _budget.Current.Action.FreeRollCount;
            int remaining = _budget.Current.FreeRollsRemaining;
            if (remaining < 0) remaining = 0;
            SetCount(remaining, total);
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

            if (_budget == null)
            {
                _extraRollButton.interactable = false;
                return;
            }

            var query = _budget.QueryExtraRoll(_playerGuid);
            _extraRollButton.interactable = query.IsAvailable;
        }

        private void RefreshButtonText()
        {
            if (_buttonLabel == null) return;

            // Si el budget arranco pero no se rolo nada → primer roll
            // (mismo criterio que CombatHUDView.InvokeRollOrReroll para dispatch).
            if (_budget != null && _budget.Current != null && _budget.Current.Action != null
                && _budget.Current.FreeRollsRemaining == _budget.Current.Action.FreeRollCount
                && _budget.Current.PaidRollsUsed == 0)
            {
                _buttonLabel.text = _firstRollText;
                return;
            }

            if (_budget == null)
            {
                _buttonLabel.text = _firstRollText;
                return;
            }

            // Sino, es reroll — gratis si quedan free, paid si toca energia.
            var query = _budget.QueryExtraRoll(_playerGuid);
            if (query.IsFreeRoll) _buttonLabel.text = _rerollFreeText;
            else if (query.CostsEnergy) _buttonLabel.text = _rerollPaidText;
            else _buttonLabel.text = _rerollFreeText;
        }
    }
}
