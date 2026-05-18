using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Orquesta los 4 botones de behavior del HUD de combate. Cada slot expone
    /// un mini state machine via <see cref="ActionButton"/>; este view escucha
    /// los eventos del bus (turn, roll, chain, behavior executed) y traduce el
    /// estado global a un <see cref="ActionButtonState"/> por slot.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Player Action Buttons View")]
    public class PlayerActionButtonsView : MonoBehaviour
    {
        // ======================================================================
        // Serialized fields — behavior buttons
        // ======================================================================

        [Title("Behavior Buttons (orden fijo: Movement / BaseAttack / SpecialAttack / Healing)")]
        [InfoBox("Cada ActionButton conoce su slot. El orden debe matchear el index 0-3 " +
                 "que CombatHandoffService espera al disparar OnBehaviorSelected.")]
        [SerializeField]
        private ActionButton[] _buttons = new ActionButton[4];

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

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        [ShowInInspector, ReadOnly]
        private bool _isPlayerTurn;

        [ShowInInspector, ReadOnly]
        private bool _inChain;

        // True entre OnDiceRolled y OnRollResolved (fuera de chain) — mientras
        // vemos los dados, no se puede cambiar de accion ni clickear otros slots.
        [ShowInInspector, ReadOnly]
        private bool _rolled;

        // Slot pressed actualmente (Selected visual). Null si no hay seleccion.
        // Limpia al ejecutarse (OnBehaviorExecuted) o cambia por cancel-by-reselection.
        [ShowInInspector, ReadOnly]
        private int? _selectedSlot;

        private IMovementService _movementService;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                int captured = i;
                _buttons[i].OnClicked += () => HandleBehaviorClick(captured);
            }

            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmClick);
        }

        private void OnDestroy()
        {
            // ActionButton se desbindea solo en su OnDestroy via RemoveListener; aca
            // limpiamos las suscripciones a su event C# por las dudas (si el view se
            // destruye antes que los botones).
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null) _buttons[i].OnClicked = null;
            }

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
            EventManager.Subscribe(EventName.OnChainStarted, HandleChainStarted);
            EventManager.Subscribe(EventName.OnChainCompleted, HandleChainCompleted);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, HandleBehaviorExecuted);
            EventManager.Subscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);

            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
            {
                _movementService = movement;
                _movementService.OnEntityMoved += HandleEntityMoved;
            }

            _bound = true;
            _isPlayerTurn = false;
            _inChain = false;
            _rolled = false;
            _selectedSlot = null;

            RefreshCostLabels();

            if (ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder)
                && turnOrder != null
                && turnOrder.ParticipantCount > 0
                && turnOrder.Current == _playerGuid)
            {
                _isPlayerTurn = true;
            }

            RecomputeButtonStates();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnChainStarted, HandleChainStarted);
            EventManager.UnSubscribe(EventName.OnChainCompleted, HandleChainCompleted);
            EventManager.UnSubscribe(EventName.OnBehaviorExecuted, HandleBehaviorExecuted);
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);

            if (_movementService != null)
            {
                _movementService.OnEntityMoved -= HandleEntityMoved;
                _movementService = null;
            }

            _bound = false;
            _isPlayerTurn = false;
            _inChain = false;
            _rolled = false;
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        // ======================================================================
        // Event handlers — bus
        // ======================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _isPlayerTurn = true;
            _inChain = false;
            _rolled = false;
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _isPlayerTurn = false;
            _inChain = false;
            _rolled = false;
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _rolled = true;
            RecomputeButtonStates();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            // Durante un chain, OnRollResolved se dispara entre fases — la accion NO
            // termino. Mantenemos _rolled tal cual y esperamos OnChainCompleted.
            if (_inChain) return;

            _rolled = false;
            RecomputeButtonStates();
        }

        private void HandleChainStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _inChain = true;
            RecomputeButtonStates();
        }

        private void HandleChainCompleted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _inChain = false;
            _rolled = false;
            // _selectedSlot lo limpia OnBehaviorExecuted; si no llega (chain con pass
            // total y phasesCompleted==0), igual queremos liberar la seleccion visual.
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        private void HandleBehaviorExecuted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            // Limpia la seleccion: el slot que se ejecuto ahora sera Used o Available
            // (segun BlockOnRepeat) en el proximo RecomputeButtonStates.
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        private void HandleInventoryChanged(params object[] args)
        {
            RecomputeButtonStates();
        }

        private void HandlePlayerEnergyChanged(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RecomputeButtonStates();
        }

        private void HandleEntityMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            // Cualquier movimiento puede cambiar la disponibilidad (range-based attack
            // entra/sale de rango). El gate _isPlayerTurn dentro del recompute evita
            // que esto habilite slots fuera del turno del jugador.
            RecomputeButtonStates();
        }

        // ======================================================================
        // Click handler
        // ======================================================================

        private void HandleBehaviorClick(int index)
        {
            // El service decide aceptar/rechazar. El visual de Selected lo seteamos
            // optimisticamente: si el service rechaza, el proximo evento (OnTurnStarted
            // o el roll/chain de otra accion) resincroniza. Cancel-by-reselection
            // del service tambien limpia su lado.
            OnBehaviorSelected?.Invoke(index);
            _selectedSlot = index;
            RecomputeButtonStates();
        }

        private void HandleConfirmClick()
        {
            // Si hay un ActionRoll activo (Heal / Forzar Puerta), Confirm = resolver la
            // tirada actual via el service. NO disparar el flow normal de combate
            // (CombatHandoffService.OnConfirmRequested) — eso ejecutaria el behavior dos veces.
            if (ServiceLocator.TryGetService<IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                rs.DeclineReroll();
                return;
            }
            _onConfirmPressed?.Invoke();
        }

        // ======================================================================
        // State recomputation
        // ======================================================================

        public void RecomputeButtonStates()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                _buttons[i].SetState(ComputeStateForSlot(i));
            }

            // Confirm se habilita solo cuando hay dados rollados pendientes de confirm
            // y NO estamos en chain (durante chain el confirm pasa a la siguiente fase
            // y los dados de chain se manejan por el chain pass / end turn).
            if (_confirmButton != null)
                _confirmButton.interactable = _isPlayerTurn && _rolled;
        }

        private ActionButtonState ComputeStateForSlot(int slotIndex)
        {
            if (!_isPlayerTurn)
                return ActionButtonState.Locked;

            // El slot seleccionado mantiene visual Selected aunque estemos en chain
            // o rolled — el jugador ve "esta es la accion que estoy ejecutando".
            if (_selectedSlot == slotIndex) return ActionButtonState.Selected;

            var behavior = ResolveBehaviorForSlot(slotIndex);
            if (behavior == null)
                return ActionButtonState.Locked;

            // Used: ejecutada con exito y BlockOnRepeat=true. TurnManager es la
            // fuente de verdad — respeta el flag de cada ActionDefinition.
            if (behavior.BlockOnRepeat && WasUsedThisTurn(behavior.ActionName))
                return ActionButtonState.Used;

            // Chain o roll en curso de OTRO slot: los demas estan lockeados para no
            // dejar al jugador iniciar una accion en paralelo.
            if (_inChain)
                return ActionButtonState.Locked;
            if (_rolled)
                return ActionButtonState.Locked;

            if (!behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _))
                return ActionButtonState.Locked;

            if (!HasEnoughEnergy(behavior))
                return ActionButtonState.Locked;

            return ActionButtonState.Available;
        }

        // ======================================================================
        // Helpers — service resolution
        // ======================================================================

        private HeroActionBehavior ResolveBehaviorForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _buttons.Length) return null;
            var button = _buttons[slotIndex];
            if (button == null) return null;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps?.CurrentHero == null)
                return null;

            return ps.CurrentHero.ResolveBaseBehavior(button.Slot, GamePhase.Combat);
        }

        private static bool WasUsedThisTurn(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return false;
            if (!ServiceLocator.TryGetService<TurnManager>(out var tm) || tm == null) return false;
            return tm.WasUsedThisTurn(actionName);
        }

        private bool HasEnoughEnergy(HeroActionBehavior behavior)
        {
            if (behavior.EnergyCost <= 0) return true;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
                return true; // sin servicio de energia, no bloqueamos en UI
            return energy.GetCurrent(_playerGuid) >= behavior.EnergyCost;
        }

        // ======================================================================
        // Cost labels
        // ======================================================================

        public void RefreshCostLabels()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService?.CurrentHero == null)
            {
                for (int i = 0; i < _buttons.Length; i++)
                    _buttons[i]?.RefreshCostLabel(null);
                return;
            }

            var hero = playerService.CurrentHero;
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                var behavior = hero.ResolveBaseBehavior(_buttons[i].Slot, GamePhase.Combat);
                if (behavior == null)
                    _buttons[i].RefreshCostLabel(behavior);
                else
                    _buttons[i].RefreshCostLabel(ResolveDisplayCost(behavior));
            }
        }

        // Si el behavior tiene un IActionRollEffect, el cobro real lo hace el
        // IActionRollService con el cost del spec — el behavior.EnergyCost queda
        // enganoso (los wirings legacy lo ponen en 2 cuando el real es 1). Para
        // que el label refleje lo que efectivamente se va a cobrar, priorizamos
        // el spec del effect.
        private int ResolveDisplayCost(HeroActionBehavior behavior)
        {
            if (TryFindActionRollSpec(behavior, out var spec))
                return spec.EnergyCost;
            return behavior.EnergyCost;
        }

        private bool TryFindActionRollSpec(HeroActionBehavior behavior, out ActionRollSpec spec)
        {
            spec = default;
            if (behavior?.Effects == null) return false;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IActionRollEffect rollEffect
                        && rollEffect.TryGetRollSpec(_playerGuid, out spec))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
