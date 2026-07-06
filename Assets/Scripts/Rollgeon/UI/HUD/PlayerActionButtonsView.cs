using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects.Concretes;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Input;
using Rollgeon.Movement;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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

        [Tooltip("DiceZoneView del HUD compartido. Se usa para chequear si hay al menos " +
                 "un dado holdeado antes de habilitar Confirm. Auto-resolve si null en Bind.")]
        [SerializeField]
        private DiceZoneView _diceZone;

        // ======================================================================
        // Events
        // ======================================================================

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onConfirmPressed = new UnityEvent();

        public UnityEvent OnConfirmPressed => _onConfirmPressed;

        public Action<int> OnBehaviorSelected;

        /// <summary>Slot actualmente Selected (pressed), o null si no hay selección.
        /// Lo consume <see cref="CombatHudZoneFlow"/> para saber qué chip mover al
        /// anchor de "acción activa" durante la fase de dados (CNF-007).</summary>
        public int? SelectedSlot => _selectedSlot;

        /// <summary>Botón del slot pedido, o null si el índice está fuera de rango.</summary>
        public ActionButton GetButtonAt(int slotIndex)
            => slotIndex >= 0 && slotIndex < _buttons.Length ? _buttons[slotIndex] : null;

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

        // True mientras una accion sin tirada (ej. Movement) espera que el jugador
        // elija el tile destino. La accion ya se comprometio pero ejecuta async; sin
        // este lock los demas slots quedarian Available y el jugador podria disparar
        // otra accion en paralelo al movimiento (BUG-013). Lo setea
        // OnActionSelectionStarted y lo limpia OnBehaviorExecuted.
        [ShowInInspector, ReadOnly]
        private bool _awaitingSelection;

        // Slot pressed actualmente (Selected visual). Null si no hay seleccion.
        // Limpia al ejecutarse (OnBehaviorExecuted) o cambia por cancel-by-reselection.
        [ShowInInspector, ReadOnly]
        private int? _selectedSlot;

        private IMovementService _movementService;
        private IGameplayHotkeyService _hotkeys;

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
            EventManager.Subscribe(EventName.OnActionSelectionStarted, HandleActionSelectionStarted);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, HandleBehaviorExecuted);
            EventManager.Subscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);
            TypedEvent<ComboMatchedPayload>.Subscribe(HandleComboMatchedForConfirm);

            HookHotkeys(true);

            if (_diceZone == null) _diceZone = UnityEngine.Object.FindFirstObjectByType<DiceZoneView>();

            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
            {
                _movementService = movement;
                _movementService.OnEntityMoved += HandleEntityMoved;
            }

            _bound = true;
            _isPlayerTurn = false;
            _inChain = false;
            _rolled = false;
            _awaitingSelection = false;
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
            EventManager.UnSubscribe(EventName.OnActionSelectionStarted, HandleActionSelectionStarted);
            EventManager.UnSubscribe(EventName.OnBehaviorExecuted, HandleBehaviorExecuted);
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(HandleComboMatchedForConfirm);

            HookHotkeys(false);

            if (_movementService != null)
            {
                _movementService.OnEntityMoved -= HandleEntityMoved;
                _movementService = null;
            }

            _bound = false;
            _isPlayerTurn = false;
            _inChain = false;
            _rolled = false;
            _awaitingSelection = false;
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
            _awaitingSelection = false;
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
            _awaitingSelection = false;
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
            _awaitingSelection = false;
            // _selectedSlot lo limpia OnBehaviorExecuted; si no llega (chain con pass
            // total y phasesCompleted==0), igual queremos liberar la seleccion visual.
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        // Una accion sin tirada (Movement) quedo comprometida y espera el click del tile
        // destino. Lockeamos los demas slots hasta que termine (OnBehaviorExecuted la
        // libera) — sin esto el jugador podria atacar mientras el movimiento esta pendiente
        // y ambas acciones corrian en paralelo (BUG-013).
        private void HandleActionSelectionStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            _awaitingSelection = true;
            RecomputeButtonStates();
        }

        private void HandleBehaviorExecuted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;

            // Limpia la seleccion: el slot que se ejecuto ahora sera Used o Available
            // (segun BlockOnRepeat) en el proximo RecomputeButtonStates. Tambien libera el
            // lock de seleccion pendiente (BUG-013) — la accion async ya termino.
            _awaitingSelection = false;
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

        // DiceZoneView dispara TypedEvent<ComboMatchedPayload> en cada toggle de hold.
        // Lo usamos como hook para recomputar el Confirm — gate del Confirm requiere
        // que haya al menos un dado holdeado, así que cada cambio de holds dispara
        // un recompute para reflejar el estado.
        private void HandleComboMatchedForConfirm(ComboMatchedPayload _)
        {
            RecomputeButtonStates();
        }

        // ======================================================================
        // Click handler
        // ======================================================================

        private void HandleBehaviorClick(int index)
        {
            // BUG-013: re-click del slot que ya está esperando su tile (Movement) = cancelar.
            // El handoff resetea el estado vía OnBehaviorExecuted DENTRO del Invoke, así que
            // no debemos re-seleccionar el slot después (dejaría el botón Selected tras el
            // cancel). Capturamos la condición antes del Invoke porque éste limpia el estado.
            bool cancelClick = _awaitingSelection && _selectedSlot == index;

            // El service decide aceptar/rechazar. El visual de Selected lo seteamos
            // optimisticamente: si el service rechaza, el proximo evento (OnTurnStarted
            // o el roll/chain de otra accion) resincroniza. Cancel-by-reselection
            // del service tambien limpia su lado.
            OnBehaviorSelected?.Invoke(index);
            if (cancelClick) return;

            _selectedSlot = index;
            RecomputeButtonStates();
        }

        // ======================================================================
        // Hotkeys (teclado) — mirror del click, gateado por interactable
        // ======================================================================

        // Suscribe/desuscribe las teclas a los mismos paths que los botones. Un
        // hotkey solo dispara si el botón está interactable, así hereda el gating
        // (turno, energía, chain, once-per-turn) sin duplicar reglas.
        private void HookHotkeys(bool subscribe)
        {
            if (subscribe)
            {
                if (_hotkeys == null
                    && !ServiceLocator.TryGetService<IGameplayHotkeyService>(out _hotkeys))
                    return;
            }
            if (_hotkeys == null) return;

            if (subscribe)
            {
                _hotkeys.Subscribe(GameplayHotkey.Move, OnHotkeyMove);
                _hotkeys.Subscribe(GameplayHotkey.Attack, OnHotkeyAttack);
                _hotkeys.Subscribe(GameplayHotkey.SpecialAttack, OnHotkeySpecial);
                _hotkeys.Subscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                _hotkeys.Subscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
                _hotkeys.Subscribe(GameplayHotkey.Confirm, OnHotkeyConfirm);
            }
            else
            {
                _hotkeys.Unsubscribe(GameplayHotkey.Move, OnHotkeyMove);
                _hotkeys.Unsubscribe(GameplayHotkey.Attack, OnHotkeyAttack);
                _hotkeys.Unsubscribe(GameplayHotkey.SpecialAttack, OnHotkeySpecial);
                _hotkeys.Unsubscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                _hotkeys.Unsubscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
                _hotkeys.Unsubscribe(GameplayHotkey.Confirm, OnHotkeyConfirm);
                _hotkeys = null;
            }
        }

        private void OnHotkeyMove(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Movement);
        private void OnHotkeyAttack(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.BaseAttack);
        private void OnHotkeySpecial(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.SpecialAttack);
        private void OnHotkeyHeal(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Healing);
        private void OnHotkeyForceDoor(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.ForceDoor);

        private void OnHotkeyConfirm(InputAction.CallbackContext _)
        {
            if (_confirmButton != null && _confirmButton.interactable)
                _confirmButton.onClick.Invoke();
        }

        // Invoca el onClick del ActionButton cuyo Slot matchea (mismo path que un
        // click real → HandleBehaviorClick con el index correcto). No-op si el botón
        // no está interactable.
        private void TriggerSlotHotkey(HeroBehaviorSlot slot)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button == null || button.Slot != slot) continue;
                if (button.Button != null && button.Button.interactable)
                    button.Button.onClick.Invoke();
                return;
            }
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

            // Confirm se habilita cuando hay dados rolleados AND el jugador holdeó
            // al menos un dado. Sin holds confirmar no tiene sentido (no hay combo
            // posible), y el botón quedaría engañando al usuario.
            if (_confirmButton != null)
                _confirmButton.interactable = _isPlayerTurn && _rolled && AnyDieHeld();
        }

        private bool AnyDieHeld()
        {
            if (_diceZone == null) return false;
            var holds = _diceZone.GetHeldStates();
            if (holds == null) return false;
            for (int i = 0; i < holds.Length; i++)
                if (holds[i]) return true;
            return false;
        }

        private ActionButtonState ComputeStateForSlot(int slotIndex)
        {
            // [DIAG temporal] Logueamos el motivo de cada Locked/Used para pinpointear
            // el bug de "botón sigue activo tras usar" y "boss: botones grises con energía".
            // Quitar estos Debug.Log una vez diagnosticado.
            if (!_isPlayerTurn)
            {
                return ActionButtonState.Locked;
            }

            // El slot seleccionado mantiene visual Selected aunque estemos en chain
            // o rolled — el jugador ve "esta es la accion que estoy ejecutando".
            if (_selectedSlot == slotIndex) return ActionButtonState.Selected;

            var behavior = ResolveBehaviorForSlot(slotIndex);
            if (behavior == null)
            {
                return ActionButtonState.Locked;
            }

            // BUG-018: en COMBATE TODA acción es once-per-turn. El asset legacy de
            // Forzar Puerta tenía BlockOnRepeat=0 (permitía retry tras fallo); el
            // resto del flow ya hace MarkBehaviorUsed via CombatHandoffService, así
            // que acá ignoramos BlockOnRepeat y gateamos sólo por WasUsedThisTurn.
            if (WasUsedThisTurn(behavior.ActionName))
            {
                return ActionButtonState.Used;
            }

            // Chain, roll, o seleccion de target pendiente de OTRO slot: los demas estan
            // lockeados para no dejar al jugador iniciar una accion en paralelo. El
            // _awaitingSelection cubre el caso de Movement (BUG-013), que no rola dados.
            if (_inChain)
                return ActionButtonState.Locked;
            if (_rolled)
                return ActionButtonState.Locked;
            if (_awaitingSelection)
                return ActionButtonState.Locked;

            // Force Door es contextual: solo habilita pegado (Manhattan ≤ 1, ortogonal)
            // a una puerta no-tapiada y FUERA de la sala de Boss (sin escape). PCAdjacentToDoor
            // vive en ShowConditions, que HasUsableEffectGroup NO evalúa — sin este gate el
            // botón quedaría Available en cualquier lado de la sala con energía suficiente.
            // HandleEntityMoved → RecomputeButtonStates ya reactiva esto al moverse.
            if (behavior.Slot == HeroBehaviorSlot.ForceDoor
                && !EffForceDoor.CanAttemptForceDoor(_playerGuid))
                return ActionButtonState.Locked;

            // BUG-017: con la vida llena el heal no aporta nada (HealPipeline lo
            // clampea a 0) — el botón debe quedar Locked para no gastar el turno.
            if (behavior.Slot == HeroBehaviorSlot.Healing
                && !HealAvailability.CanHealMore(_playerGuid))
            {
                return ActionButtonState.Locked;
            }

            if (!behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out var usableReason))
            {
                return ActionButtonState.Locked;
            }

            if (!HasEnoughEnergy(behavior))
            {
                int cur = ServiceLocator.TryGetService<IEnergyService>(out var es) && es != null
                    ? es.GetCurrent(_playerGuid) : -1;
                return ActionButtonState.Locked;
            }

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
        // enganoso (los wirings legacy lo ponen en 2 cuando el real es 1). Regla
        // compartida con el texto de tooltips (HeroActionTooltip).
        private int ResolveDisplayCost(HeroActionBehavior behavior)
            => Rollgeon.UI.Tooltips.HeroActionTooltip.ResolveDisplayCost(behavior, _playerGuid);
    }
}
