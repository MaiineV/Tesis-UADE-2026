using System;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que renderiza los 3 botones del panel de acciones: Atacar / Energia
    /// extra (reroll) / Terminar turno. El enable/disable sale de consultar al
    /// <see cref="TurnManager"/> y a <see cref="IRerollBudgetService"/>, y del flag
    /// interno <c>_isPlayerTurn</c> derivado de <c>OnTurnStarted/Finished</c>.
    /// Plan §3.5 / §4.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los botones NO dispatchan directo al <see cref="TurnManager"/>. Exponen
    /// <see cref="UnityEvent"/>s (<c>OnAttackPressed</c>, <c>OnEnergyRerollPressed</c>,
    /// <c>OnEndTurnPressed</c>) que la screen padre (<see cref="Rollgeon.UI.Screens.CombatHUDView"/>)
    /// cablea a delegates <c>Action</c> inyectados por el <c>CombatController</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Buttons View")]
    public class ActionButtonsView : MonoBehaviour
    {
        private const string LogPrefix = "[ActionButtonsView] ";

        [Title("Action Buttons — Widgets")]
        [Required("Arrastrar el boton de Atacar.")]
        [SerializeField]
        private Button _attackButton;

        [Required("Arrastrar el boton de Energy Reroll.")]
        [SerializeField]
        private Button _energyRerollButton;

        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

        [Title("Action Buttons — Config")]
        [SerializeField]
        [Tooltip("ActionDefinitionSO del ataque basico del guerrero. Se pasa al " +
                 "TurnManager.CanExecute para gating. Null = siempre deshabilitado.")]
        private ActionDefinitionSO _attackAction;

        [Title("Action Buttons — Events")]
        [SerializeField]
        private UnityEvent _onAttackPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onEnergyRerollPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onEndTurnPressed = new UnityEvent();

        public UnityEvent OnAttackPressed => _onAttackPressed;
        public UnityEvent OnEnergyRerollPressed => _onEnergyRerollPressed;
        public UnityEvent OnEndTurnPressed => _onEndTurnPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _isPlayerTurn;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private void Awake()
        {
            if (_attackButton != null) _attackButton.onClick.AddListener(HandleAttackClick);
            if (_energyRerollButton != null) _energyRerollButton.onClick.AddListener(HandleRerollClick);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurnClick);
        }

        private void OnDestroy()
        {
            if (_attackButton != null) _attackButton.onClick.RemoveListener(HandleAttackClick);
            if (_energyRerollButton != null) _energyRerollButton.onClick.RemoveListener(HandleRerollClick);
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleEndTurnClick);
        }

        /// <summary>Suscribe al bus. Arranca con botones deshabilitados hasta el primer OnTurnStarted.</summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnEnergyChanged, HandleEnergyChanged);
            EventManager.Subscribe(EventName.OnRerollBudgetChanged, HandleRerollBudgetChanged);
            _bound = true;

            _isPlayerTurn = false;
            RefreshInteractable();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnEnergyChanged, HandleEnergyChanged);
            EventManager.UnSubscribe(EventName.OnRerollBudgetChanged, HandleRerollBudgetChanged);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica (tests / tooling)
        // ======================================================================

        /// <summary>Fuerza refresco de los interactable states.</summary>
        public void RefreshInteractable()
        {
            bool playerTurn = _isPlayerTurn;

            // Attack — requiere player turn + CanExecute del TurnManager.
            bool canAttack = playerTurn && CanExecuteAttack();
            if (_attackButton != null) _attackButton.interactable = canAttack;

            // Reroll — requiere player turn + IRerollBudgetService disponible.
            bool canReroll = playerTurn && CanExecuteReroll();
            if (_energyRerollButton != null) _energyRerollButton.interactable = canReroll;

            // End turn — solo requiere player turn.
            if (_endTurnButton != null) _endTurnButton.interactable = playerTurn;
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleAttackClick()
        {
            if (_onAttackPressed == null)
            {
                Debug.LogWarning(LogPrefix + "OnAttackPressed UnityEvent es null.", this);
                return;
            }
            _onAttackPressed.Invoke();
        }

        private void HandleRerollClick()
        {
            _onEnergyRerollPressed?.Invoke();
        }

        private void HandleEndTurnClick()
        {
            _onEndTurnPressed?.Invoke();
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid))
            {
                return;
            }
            _isPlayerTurn = (guid == _playerGuid);
            RefreshInteractable();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid))
            {
                return;
            }
            if (guid == _playerGuid)
            {
                _isPlayerTurn = false;
                RefreshInteractable();
            }
        }

        private void HandleEnergyChanged(params object[] args)
        {
            // Solo nos interesa re-evaluar cuando cambia la energia del player
            // (afecta CanExecute del attack y del reroll).
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshInteractable();
        }

        private void HandleRerollBudgetChanged(params object[] args)
        {
            // [STUB T104] schema: [Guid playerGuid, int used, int cap]
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshInteractable();
        }

        // ======================================================================
        // Gating helpers
        // ======================================================================

        private bool CanExecuteAttack()
        {
            if (_attackAction == null) return false;
            if (!ServiceLocator.TryGetService<TurnManager>(out var tm) || tm == null)
            {
                // Sin TurnManager, fallback permisivo: asumimos que si es turno del player,
                // puede atacar. Esto es consistente con la graceful degradation que
                // describe el plan §3.5.
                return true;
            }
            return tm.CanExecute(_attackAction, _playerGuid, out _);
        }

        private bool CanExecuteReroll()
        {
            if (!ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) || budget == null)
            {
                // [STUB T104] — si el servicio no esta registrado, deshabilitamos
                // el boton con "T104 pending" (por tooltip del prefab, setup doc).
                return false;
            }

            var query = budget.QueryExtraRoll(_playerGuid);
            return query.IsAvailable;
        }
    }
}
