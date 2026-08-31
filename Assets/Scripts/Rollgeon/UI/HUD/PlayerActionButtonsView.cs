using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects.Concretes;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Input;
using Rollgeon.Movement;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

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

        [Title("Behavior Buttons (orden fijo: Movement / BaseAttack / ClassSkill / Healing / ForceDoor / Defense)")]
        [InfoBox("Cada ActionButton conoce su slot. El orden debe matchear el index " +
                 "que CombatHandoffService espera al disparar OnBehaviorSelected " +
                 "(índice de array == valor de HeroBehaviorSlot).")]
        [SerializeField]
        private ActionButton[] _buttons = new ActionButton[6];

        // ======================================================================
        // Events
        // ======================================================================

        // El botón Confirm ya no vive acá: lo absorbió el botón contextual de turno
        // (EndTurnButtonView, modo Confirm) — este view solo maneja los 4 chips.

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
                _buttons[i].OnRejected += () => HandleBehaviorRejected(captured);
                _buttons[i].OnBlockedPressed += _ => ShowRejectToast(captured);
            }
        }

        private void OnDestroy()
        {
            // ActionButton se desbindea solo en su OnDestroy via RemoveListener; aca
            // limpiamos las suscripciones a su event C# por las dudas (si el view se
            // destruye antes que los botones).
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                _buttons[i].OnClicked = null;
                _buttons[i].OnRejected = null;
                _buttons[i].OnBlockedPressed = null;
            }
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
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandlePlayerRollsChanged);
            EventManager.Subscribe(EventName.OnTutorialActionUnlocked, HandleTutorialActionUnlocked);
            EventManager.Subscribe(EventName.OnPhaseEnter, HandlePhaseEnter);

            HookHotkeys(true);

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

        /// <summary>
        /// RectTransform del botón de un slot — usado por el overlay del tutorial
        /// para recortar/señalar el botón. <c>false</c> si el slot no está cableado.
        /// </summary>
        public bool TryGetButtonRect(HeroBehaviorSlot slot, out RectTransform rect)
        {
            rect = null;
            for (int i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button == null || button.Slot != slot) continue;
                rect = button.transform as RectTransform;
                return rect != null;
            }
            return false;
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
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandlePlayerRollsChanged);
            EventManager.UnSubscribe(EventName.OnTutorialActionUnlocked, HandleTutorialActionUnlocked);
            EventManager.UnSubscribe(EventName.OnPhaseEnter, HandlePhaseEnter);

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

            // Los costos contextuales (Heal, Forzar Puerta) valen distinto dentro y fuera
            // de combate, y el Bind puede correr antes de que la fase esté seteada. Para
            // cuando el jugador puede actuar, el número tiene que ser el bueno.
            RefreshCostLabels();
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

            // Limpia la seleccion: el slot que se ejecuto vuelve a la cascada normal en el
            // proximo RecomputeButtonStates (sin limite de acciones por turno — solo gatea
            // el pool de rolls). Tambien libera el lock de seleccion pendiente (BUG-013) —
            // la accion async ya termino.
            _awaitingSelection = false;
            _selectedSlot = null;
            RecomputeButtonStates();
        }

        private void HandleInventoryChanged(params object[] args)
        {
            RecomputeButtonStates();
        }

        private void HandlePlayerRollsChanged(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RecomputeButtonStates();
        }

        // Tutorial: un slot recién desbloqueado debe pasar de Locked a Available
        // sin esperar otro evento de turno.
        private void HandleTutorialActionUnlocked(params object[] args)
        {
            RecomputeButtonStates();
        }

        // Los costos contextuales (Heal, Forzar Puerta) los resuelve el spec del effect
        // preguntándole la fase VIVA al IPhaseService, así que fuera de combate valen 0.
        // El Bind del HUD corre antes de que la fase sea Combat: sin este refresh el
        // label del heal quedaba en el _zeroCostText (vacío) todo el combate, y el gate
        // de energía lo trataba como gratis. PhaseService setea CurrentBase ANTES de
        // disparar OnPhaseEnter, así que acá ya se lee el valor nuevo.
        private void HandlePhaseEnter(params object[] args)
        {
            RefreshCostLabels();
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

            // El botón solo es clickeable si su estado lo permite (gating incluido),
            // así que esto anuncia una selección efectiva — el tutorial lo usa para
            // encadenar el paso siguiente (p.e. señalar los dados).
            if (index >= 0 && index < _buttons.Length && _buttons[index] != null)
                EventManager.Trigger(EventName.OnHeroBehaviorClicked, _buttons[index].Slot);
        }

        // El chip avisa que lo intentaron usar sin rolls; nosotros somos los que
        // sabemos de quien es, asi que enriquecemos y publicamos. La pila de rolls
        // escucha el evento — vive en otro prefab y con otro ciclo de vida, asi
        // que una ref directa seria fragil.
        private void HandleBehaviorRejected(int index)
        {
            var behavior = ResolveBehaviorForSlot(index);
            if (behavior == null) return;

            int current = ServiceLocator.TryGetService<IRollPoolService>(out var rolls) && rolls != null
                ? rolls.GetCurrent(_playerGuid)
                : 0;

            TypedEvent<InsufficientRollsPayload>.Raise(new InsufficientRollsPayload
            {
                PlayerGuid = _playerGuid,
                Cost = 1,
                Current = current,
            });
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
                _hotkeys.Subscribe(GameplayHotkey.ClassSkill, OnHotkeyClassSkill);
                _hotkeys.Subscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                _hotkeys.Subscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
                _hotkeys.Subscribe(GameplayHotkey.Defense, OnHotkeyDefense);
            }
            else
            {
                _hotkeys.Unsubscribe(GameplayHotkey.Move, OnHotkeyMove);
                _hotkeys.Unsubscribe(GameplayHotkey.Attack, OnHotkeyAttack);
                _hotkeys.Unsubscribe(GameplayHotkey.ClassSkill, OnHotkeyClassSkill);
                _hotkeys.Unsubscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                _hotkeys.Unsubscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
                _hotkeys.Unsubscribe(GameplayHotkey.Defense, OnHotkeyDefense);
                _hotkeys = null;
            }
        }

        private void OnHotkeyMove(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Movement);
        private void OnHotkeyAttack(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.BaseAttack);
        private void OnHotkeyClassSkill(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.ClassSkill);
        private void OnHotkeyHeal(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Healing);
        private void OnHotkeyForceDoor(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.ForceDoor);
        private void OnHotkeyDefense(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Defense);

        // Invoca el onClick del ActionButton cuyo Slot matchea (mismo path que un
        // click real → HandleBehaviorClick con el index correcto). Si el botón está
        // bloqueado (sin energía, locked, usado), la tecla responde con el MISMO
        // rechazo completo que el mouse — shake + SFX + toast vía TryRejectPress,
        // que es el camino de OnPointerDown.
        private void TriggerSlotHotkey(HeroBehaviorSlot slot)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button == null || button.Slot != slot) continue;
                if (button.Button != null && button.Button.interactable)
                    button.Button.onClick.Invoke();
                else
                    button.TryRejectPress();
                return;
            }
        }

        // ======================================================================
        // State recomputation
        // ======================================================================

        public void RecomputeButtonStates()
        {
            // Aparte del estado: el estado es excluyente y se queda con la PRIMERA
            // razon de la cascada, asi que un chip Locked por rango o por vida llena
            // ocultaba que ademas no lo podias pagar. El flag solo se enciende cuando
            // la falta de rolls es LA noticia: turno propio, sin accion en vuelo y sin
            // modal — pintar rojo durante la animacion propia o el turno enemigo es
            // ruido (BUG-074).
            bool modalRollActive = _awaitingSelection
                && ServiceLocator.TryGetService<IActionRollService>(out var modalRoll)
                && modalRoll != null && modalRoll.IsActive;
            bool flagUnaffordable = ShouldFlagUnaffordable(
                _isPlayerTurn, _inChain, _rolled, modalRollActive, HasEnoughRolls());

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;

                var behavior = ResolveBehaviorForSlot(i);
                _buttons[i].SetState(ComputeStateForSlot(i, behavior));

                // Sin behavior no opinamos.
                if (behavior != null) _buttons[i].SetAffordable(!flagUnaffordable);
            }
        }

        /// <summary>
        /// Regla pura del flag ortogonal de affordability (BUG-074): rojo en TODAS las
        /// fichas con behavior cuando es el turno del jugador, no hay accion en curso ni
        /// modal abierto, y el pool no cubre ni 1 roll.
        /// </summary>
        internal static bool ShouldFlagUnaffordable(
            bool isPlayerTurn, bool inChain, bool rolled, bool modalRollActive, bool hasRolls)
        {
            return isPlayerTurn && !inChain && !rolled && !modalRollActive && !hasRolls;
        }

        private ActionButtonState ComputeStateForSlot(int slotIndex, HeroActionBehavior behavior)
        {
            if (!_isPlayerTurn)
            {
                return ActionButtonState.Locked;
            }

            // Tutorial: slots que el tutorial todavía no desbloqueó quedan Locked.
            // Gate visual — el backstop de ejecución vive en
            // TurnManager.IsForbiddenByRuleset (mismo servicio).
            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var tutorialGate)
                && tutorialGate != null
                && _buttons[slotIndex] != null
                && tutorialGate.IsSlotLocked(_buttons[slotIndex].Slot))
            {
                return ActionButtonState.Locked;
            }

            // El slot seleccionado mantiene visual Selected aunque estemos en chain
            // o rolled — el jugador ve "esta es la accion que estoy ejecutando".
            if (_selectedSlot == slotIndex) return ActionButtonState.Selected;

            if (behavior == null)
            {
                return ActionButtonState.Locked;
            }

            // Chain o roll en curso: los demas slots quedan lockeados para no dejar al
            // jugador iniciar una accion en paralelo — la accion ya esta comprometida.
            if (_inChain)
                return ActionButtonState.Locked;
            if (_rolled)
                return ActionButtonState.Locked;

            // Seleccion pendiente (BUG-013/BUG-015): el panel de ActionRoll (Heal /
            // Forzar Puerta) es modal — los demas slots quedan Locked hasta resolverse.
            // La seleccion de tile de Movement NO: los demas slots siguen la cascada
            // normal (Available) y usarlos cancela el Movement y arranca la nueva
            // accion en un solo gesto (QoL switch — el handoff hace cancel-and-continue).
            if (_awaitingSelection
                && ServiceLocator.TryGetService<IActionRollService>(out var modalRoll)
                && modalRoll != null && modalRoll.IsActive)
            {
                return ActionButtonState.Locked;
            }

            // Defensa: si el handoff reporta una selección NO cancelable (throw en
            // vuelo, forced reroll), los slots quedan Locked para no prometer un
            // switch que el handoff va a ignorar. Con el dado de Movimiento ya tirado
            // la selección SÍ es cancelable (§6.6 revertido: se pierde el roll, no el
            // turno), así que este branch ya no aplica a ese caso.
            if (_awaitingSelection
                && ServiceLocator.TryGetService<Rollgeon.Combat.Handoff.ICombatHandoffService>(out var handoffSel)
                && handoffSel != null && !handoffSel.HasCancellableSelection)
            {
                return ActionButtonState.Locked;
            }

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

            // includeRollGate:false — HasUsableEffectGroup tiene su propio gate de
            // pool, y al consultarlo antes que HasEnoughRolls devolvía Locked para
            // todo chip impagable: el Unaffordable de abajo era inalcanzable. Sin ni
            // outline ni shake, que es lo que se reportó.
            if (!behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out var usableReason,
                                               includeRollGate: false))
            {
                return ActionButtonState.Locked;
            }

            // Ultimo gate de la cascada: si llegamos hasta aca todo lo demas esta
            // listo y lo unico que falta son rolls en el pool. Por eso Unaffordable
            // puede decirle al jugador POR QUE no puede — los Locked de arriba no.
            if (!HasEnoughRolls())
            {
                return ActionButtonState.Unaffordable;
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

        // Pool de Rolls: toda accion cuesta 1 roll por tirada, asi que "afordable"
        // es simplemente pool >= 1 (solo en combate; este view es del combat HUD).
        private bool HasEnoughRolls()
        {
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
                return true; // sin servicio del pool, no bloqueamos en UI
            if (!rolls.IsCombatActive) return true;
            return rolls.GetCurrent(_playerGuid) >= 1;
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
                _buttons[i].RefreshCostLabel(behavior);
            }
        }

        // ======================================================================
        // Toast de rechazo — "Esta acción no puede ser realizada" + motivo
        // ======================================================================

        private void ShowRejectToast(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _buttons.Length) return;
            var button = _buttons[slotIndex];
            if (button == null) return;

            string reason = ResolveRejectReason(button);
            if (string.IsNullOrEmpty(reason)) return;

            string title = LocalizedContent.Ui(UiTextKeys.RejectTitle,
                "Esta acción no puede ser realizada");
            ActionRejectToast.Show(button.transform as RectTransform,
                title + "\n" + reason, button.CostLabelFont);
        }

        /// <summary>
        /// Motivo del rechazo, resuelto con estado fresco al momento del tap —
        /// espejo de la cascada de <see cref="ComputeStateForSlot"/>: las condiciones
        /// (rango, puerta, vida) mandan sobre la energía, igual que Locked manda
        /// sobre Unaffordable. Null = lock transitorio (chain/roll en curso, gate
        /// del tutorial) que no merece cartel.
        /// </summary>
        private string ResolveRejectReason(ActionButton button)
        {
            if (!_isPlayerTurn)
                return LocalizedContent.Ui(UiTextKeys.RejectNotYourTurn, "No es tu turno.");

            // Slots que el tutorial todavía no desbloqueó: el overlay del tutorial ya
            // está guiando — un cartel encima solo compite con la lección.
            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var gate)
                && gate != null && gate.IsSlotLocked(button.Slot))
                return null;

            var behavior = ResolveBehaviorForSlot(System.Array.IndexOf(_buttons, button));
            if (behavior == null) return null;

            // Acción en curso: lock transitorio, sin cartel.
            if (_inChain || _rolled) return null;
            if (_awaitingSelection
                && ServiceLocator.TryGetService<IActionRollService>(out var modalRoll)
                && modalRoll != null && modalRoll.IsActive)
                return null;

            if (button.Slot == HeroBehaviorSlot.ForceDoor
                && !EffForceDoor.CanAttemptForceDoor(_playerGuid))
                return LocalizedContent.Ui(UiTextKeys.RejectNoDoor, "No estás junto a una puerta.");

            if (button.Slot == HeroBehaviorSlot.Healing
                && !HealAvailability.CanHealMore(_playerGuid))
                return LocalizedContent.Ui(UiTextKeys.RejectFullHealth, "Tienes la vida completa.");

            if (!behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _, includeRollGate: false))
                return LocalizedContent.Ui(UiTextKeys.RejectNoRange, "Sin rango al objetivo.");

            if (!HasEnoughRolls())
                return LocalizedContent.Ui(UiTextKeys.RejectNoRolls, "No te quedan Rolls.");

            return null;
        }
    }
}
