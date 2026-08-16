using System;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>Modo contextual del botón de turno del HUD de combate.</summary>
    public enum TurnButtonMode
    {
        /// <summary>Sin flujo de dados en curso — cerrar el turno.</summary>
        EndTurn = 0,

        /// <summary>Hay tirada/chain/action-roll en curso — confirmar el resultado.</summary>
        Confirm = 1,

        /// <summary>Fase de chain esperando un roll pago sin tirada hecha — pasar sin pagar.</summary>
        Pass = 2,
    }

    /// <summary>
    /// Botón contextual de turno: End Turn por defecto, Confirm mientras hay un
    /// flujo de dados en curso (chain, tirada suelta o ActionRoll en combate) y
    /// Pass cuando una fase de chain espera un roll que cuesta energía y todavía
    /// no se tiró (la salida sin pagar). Absorbe al viejo botón Confirm de la
    /// zona de dados — un solo botón, tres significados según el contexto.
    /// </summary>
    /// <remarks>
    /// El gating de Confirm replica al que tenía <c>PlayerActionButtonsView</c>:
    /// dados rolleados + al menos un hold + sin animación en vuelo. Con un
    /// ActionRoll activo (Heal en combate) manda <see cref="IActionRollService.CanConfirm"/>
    /// via polling (los holds no emiten evento en ese flow). El estado
    /// "fase paga pendiente" no viaja por el bus — lo empuja
    /// <c>CombatHUDView.Show/HideChainRollPrompt</c> vía <see cref="SetChainPaidRollPending"/>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/End Turn Button View")]
    public class EndTurnButtonView : MonoBehaviour
    {
        private const string LocTable = "UI";
        private const string KeyEndTurn = "action.end_turn";
        private const string KeyConfirm = "screen.confirm";
        private const string KeyPass = "action.pass";

        [Required("Arrastrar el boton de End Turn.")]
        [SerializeField]
        private Button _endTurnButton;

        [Tooltip("Opcional — highlight de 'sin energía' (glow + dots por el contorno). " +
                 "Si null, el botón no reacciona a la energía.")]
        [SerializeField]
        private EndTurnEnergyHighlight _energyHighlight;

        [Tooltip("Opcional — LocalizeStringEvent del label. El modo contextual le cambia " +
                 "la key (action.end_turn / screen.confirm / action.pass). Si null, el " +
                 "texto no cambia con el modo.")]
        [SerializeField]
        private LocalizeStringEvent _label;

        [Tooltip("DiceZoneView del HUD compartido — holds y animación gatean el Confirm. " +
                 "Auto-resolve si null en Bind (vive en Canvas_ActionRoll).")]
        [SerializeField]
        private DiceZoneView _diceZone;

        [Title("Sprites")]
        [Tooltip("Swap de sprites del botón por estado y contexto. Null = el modo solo " +
                 "cambia el label.")]
        [SerializeField]
        private HudButtonSpriteSwap _buttonSprites;

        [Tooltip("Arte del modo End Turn (FinishTurnButton: _1 normal, _2 hover, _0 disabled).")]
        [SerializeField]
        private ButtonSpriteSet _endTurnSprites;

        [Tooltip("Arte del modo Confirm (Confirm2: _1 normal, _0 hover).")]
        [SerializeField]
        private ButtonSpriteSet _confirmSprites;

        [Tooltip("Arte del modo Pass. Sin sheet propio por ahora — reusa FinishTurnButton.")]
        [SerializeField]
        private ButtonSpriteSet _passSprites;

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onEndTurnPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onConfirmPressed = new UnityEvent();

        [SerializeField]
        private UnityEvent _onPassPressed = new UnityEvent();

        public UnityEvent OnEndTurnPressed => _onEndTurnPressed;
        public UnityEvent OnConfirmPressed => _onConfirmPressed;
        public UnityEvent OnPassPressed => _onPassPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        [ShowInInspector, ReadOnly]
        private bool _isPlayerTurn;

        // True desde OnDiceRolled. Fuera de chain lo limpia OnRollResolved; dentro
        // de un chain queda latcheado hasta OnChainCompleted (entre fases hay
        // OnRollResolved intermedios pero la acción sigue en curso — mismo latch
        // que usaba PlayerActionButtonsView para el gating del Confirm).
        [ShowInInspector, ReadOnly]
        private bool _rolled;

        [ShowInInspector, ReadOnly]
        private bool _inChain;

        // Fase de chain abierta esperando un roll pago, sin dados en mesa. Lo
        // empuja CombatHUDView junto con el prompt "X Roll -1⚡".
        [ShowInInspector, ReadOnly]
        private bool _chainPaidRollPending;

        private TurnButtonMode _appliedMode = TurnButtonMode.EndTurn;
        private IActionRollService _actionRoll;
        private IGameplayHotkeyService _hotkeys;

        /// <summary>Modo contextual vigente — Pass gana a Confirm, Confirm a EndTurn.</summary>
        public TurnButtonMode CurrentMode
        {
            get
            {
                if (!_bound) return TurnButtonMode.EndTurn;
                if (_chainPaidRollPending) return TurnButtonMode.Pass;
                if (IsActionRollActive() || _inChain || _rolled) return TurnButtonMode.Confirm;
                return TurnButtonMode.EndTurn;
            }
        }

        /// <summary>RectTransform del botón — anchor del overlay del tutorial.</summary>
        public bool TryGetButtonRect(out RectTransform rect)
        {
            rect = _endTurnButton != null ? _endTurnButton.transform as RectTransform : null;
            return rect != null;
        }

        private void Awake()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(HandleClick);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void Update()
        {
            // CanConfirm del ActionRoll cambia con los holds y el service no emite
            // "holds changed" — polling barato solo mientras hay ActionRoll activo
            // (mismo approach que tenía ActionRollConfirmGate).
            if (_bound && IsActionRollActive()) Refresh();
        }

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
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEnd);
            // Toggle de hold y cambios de selección re-gatean el Confirm (el hold
            // habilita, el cancel de una selección post-confirm debe re-armar).
            TypedEvent<ComboMatchedPayload>.Subscribe(HandleComboMatched);
            EventManager.Subscribe(EventName.OnCombatTargetChanged, HandleCombatTargetChanged);

            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out _hotkeys) && _hotkeys != null)
                _hotkeys.Subscribe(GameplayHotkey.EndTurn, OnHotkeyEndTurn);

            if (_energyHighlight != null) _energyHighlight.Bind(playerGuid);

            // El ActionRollService es Run-scoped: la referencia se re-resuelve por
            // combate para no quedar apuntando a un service de un run anterior.
            _actionRoll = null;

            if (_diceZone == null) _diceZone = UnityEngine.Object.FindFirstObjectByType<DiceZoneView>();
            if (_diceZone != null) _diceZone.DiceAnimationStateChanged += Refresh;

            _bound = true;
            _isPlayerTurn = false;
            _rolled = false;
            _inChain = false;
            _chainPaidRollPending = false;
            Refresh();
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
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEnd);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(HandleComboMatched);
            EventManager.UnSubscribe(EventName.OnCombatTargetChanged, HandleCombatTargetChanged);

            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.EndTurn, OnHotkeyEndTurn);
                _hotkeys = null;
            }

            if (_energyHighlight != null) _energyHighlight.Unbind();
            if (_diceZone != null) _diceZone.DiceAnimationStateChanged -= Refresh;

            _bound = false;
            _isPlayerTurn = false;
            _rolled = false;
            _inChain = false;
            _chainPaidRollPending = false;
            _actionRoll = null;
            Refresh();
        }

        /// <summary>
        /// Fase de chain con entrada paga abierta (prompt "X Roll -1⚡" visible).
        /// Lo empuja <c>CombatHUDView</c> desde Show/HideChainRollPrompt — el modo
        /// pasa a Pass mientras dure.
        /// </summary>
        public void SetChainPaidRollPending(bool pending)
        {
            _chainPaidRollPending = pending;
            Refresh();
        }

        /// <summary>Recalcula modo (label + sprite) e interactable a partir del estado vigente.</summary>
        public void Refresh()
        {
            var mode = CurrentMode;
            ApplyModeLabel(mode);
            ApplyModeSprites(mode);
            if (_endTurnButton != null)
                _endTurnButton.interactable = _bound && ComputeInteractable(mode);
        }

        /// <summary>Compat con el wiring previo — hoy delega en <see cref="Refresh"/>.</summary>
        public void RefreshInteractable() => Refresh();

        // ---- Estado ------------------------------------------------------------

        private bool ComputeInteractable(TurnButtonMode mode)
        {
            switch (mode)
            {
                case TurnButtonMode.Pass:
                    return _isPlayerTurn;

                case TurnButtonMode.Confirm:
                    if (IsActionRollActive()) return _actionRoll.CanConfirm;
                    // Mismo gate que tenía PlayerActionButtonsView: sin holds no hay
                    // combo posible y con dados girando el resultado no se reveló.
                    return _isPlayerTurn && _rolled && AnyDieHeld()
                           && !(_diceZone != null && _diceZone.IsDiceAnimating);

                default:
                    return _isPlayerTurn;
            }
        }

        private void ApplyModeLabel(TurnButtonMode mode)
        {
            if (mode == _appliedMode) return;
            _appliedMode = mode;
            if (_label == null) return;
            string key = mode switch
            {
                TurnButtonMode.Confirm => KeyConfirm,
                TurnButtonMode.Pass => KeyPass,
                _ => KeyEndTurn,
            };
            _label.StringReference.SetReference(LocTable, key);
        }

        // Sin guard de modo repetido: Apply es idempotente (asignar el mismo sprite
        // no ensucia la Image) y así el primer Refresh del Bind pinta el arte inicial.
        private void ApplyModeSprites(TurnButtonMode mode)
        {
            if (_buttonSprites == null) return;
            _buttonSprites.Apply(mode switch
            {
                TurnButtonMode.Confirm => _confirmSprites,
                TurnButtonMode.Pass => _passSprites,
                _ => _endTurnSprites,
            });
        }

        private bool IsActionRollActive()
        {
            if (_actionRoll == null) ServiceLocator.TryGetService(out _actionRoll);
            return _actionRoll != null && _actionRoll.IsActive;
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

        // ---- Handlers ----------------------------------------------------------

        private bool IsForPlayer(object[] args)
            => args != null && args.Length >= 1 && args[0] is Guid guid && guid == _playerGuid;

        private void HandleTurnStarted(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _isPlayerTurn = true;
            _rolled = false;
            _inChain = false;
            _chainPaidRollPending = false;
            Refresh();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _isPlayerTurn = false;
            _rolled = false;
            _inChain = false;
            _chainPaidRollPending = false;
            Refresh();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _rolled = true;
            Refresh();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            // En chain el OnRollResolved intermedio no cierra la acción — el latch
            // cae recién con OnChainCompleted.
            if (!_inChain) _rolled = false;
            Refresh();
        }

        private void HandleChainStarted(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _inChain = true;
            Refresh();
        }

        private void HandleChainCompleted(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _inChain = false;
            _rolled = false;
            _chainPaidRollPending = false;
            Refresh();
        }

        private void HandleBehaviorExecuted(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            Refresh();
        }

        private void HandleCombatEnd(params object[] args)
        {
            _isPlayerTurn = false;
            _rolled = false;
            _inChain = false;
            _chainPaidRollPending = false;
            Refresh();
        }

        private void HandleComboMatched(ComboMatchedPayload _) => Refresh();

        private void HandleCombatTargetChanged(params object[] args) => Refresh();

        // ---- Click -------------------------------------------------------------

        private void HandleClick()
        {
            switch (CurrentMode)
            {
                case TurnButtonMode.Confirm:
                    // ActionRoll activo (Heal en combate): Confirm = resolver la tirada
                    // via el service — NO disparar el flow de combate (ejecutaría el
                    // behavior dos veces). Mismo branch que el viejo HandleConfirmClick.
                    if (IsActionRollActive())
                    {
                        _actionRoll.DeclineReroll();
                        Refresh();
                        return;
                    }
                    _onConfirmPressed?.Invoke();
                    // BUG-018: en chain el OnRollResolved que apagaría el botón viene
                    // diferido por el feedback del golpe — lo apagamos ya para que el
                    // spam ni llegue al service. El próximo Refresh con estado fresco
                    // lo re-habilita cuando corresponda.
                    if (_inChain && _endTurnButton != null)
                        _endTurnButton.interactable = false;
                    return;

                case TurnButtonMode.Pass:
                    _onPassPressed?.Invoke();
                    return;

                default:
                    _onEndTurnPressed?.Invoke();
                    return;
            }
        }

        // Space = click del botón contextual, solo si está interactable (mismo gating).
        private void OnHotkeyEndTurn(InputAction.CallbackContext _)
        {
            // Guard histórico del par Confirm/EndTurn sobre Space — hoy nadie más
            // consume el frame, pero protege si algo vuelve a compartir la tecla.
            if (_hotkeys != null && _hotkeys.WasFrameConsumed()) return;
            if (_endTurnButton != null && _endTurnButton.interactable)
                _endTurnButton.onClick.Invoke();
        }
    }
}
