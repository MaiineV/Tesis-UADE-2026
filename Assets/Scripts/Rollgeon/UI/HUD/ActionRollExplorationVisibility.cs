using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Phase;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Gateaje de visibilidad para los views compartidos de la zona de dados (DiceZoneView,
    /// RerollCountView, DamageFormulaView, ConfirmButton) que viven en el Canvas raíz.
    /// Regla (CNF-007): en <see cref="GamePhase.Combat"/> visible solo mientras hay un
    /// flujo de dados activo (entre <c>OnChainStarted</c>/<c>OnDiceRolled</c> y
    /// <c>OnBehaviorExecuted</c>) — la zona de chips y la de dados se alternan.
    /// En <see cref="GamePhase.Exploration"/> visible solo cuando hay un
    /// <see cref="IActionRollService"/> activo (heal con poción, etc.).
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Roll Exploration Visibility")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ActionRollExplorationVisibility : MonoBehaviour
    {
        [Tooltip("[DEPRECATED] Antes forzaba Button.interactable=true mientras visible. " +
                 "Eso atropellaba el gating por energía del RerollCountView (sin energía igual " +
                 "se podía clickear y se rompía el flow). Ahora default false — los views " +
                 "específicos controlan interactabilidad; este componente solo maneja visibility.")]
        [SerializeField] private bool _forceButtonInteractable;

        private CanvasGroup _group;
        private Button _ownButton;
        private IActionRollService _actionRoll;
        private System.Action<ActionRollPhase> _onPhase;
        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;

        // CNF-007: true entre el arranque del flujo de dados (chain iniciado / dados
        // tirados) y su resolución (behavior ejecutado / fin de combate / nuevo turno).
        private bool _combatDiceFlowActive;
        private EventManager.EventReceiver _onDiceFlowStart;
        private EventManager.EventReceiver _onDiceFlowEnd;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _ownButton = GetComponent<Button>();
            // Empezamos oculto por default; Refresh decide según el contexto.
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            // Phase events siempre se subscriben en OnEnable — el EventManager
            // legacy esta disponible desde antes del Run scope.
            _onPhaseEnter = _ => Refresh();
            _onPhaseExit = _ => Refresh();
            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);

            // CNF-007: en combate la zona vive solo durante el flujo de dados.
            _onDiceFlowStart = _ => { _combatDiceFlowActive = true; Refresh(); };
            _onDiceFlowEnd = _ => { _combatDiceFlowActive = false; Refresh(); };
            EventManager.Subscribe(EventName.OnChainStarted, _onDiceFlowStart);
            EventManager.Subscribe(EventName.OnDiceRolled, _onDiceFlowStart);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, _onDiceFlowEnd);
            EventManager.Subscribe(EventName.OnCombatEnd, _onDiceFlowEnd);
            EventManager.Subscribe(EventName.OnTurnStarted, _onDiceFlowEnd);

            // El IActionRollService es Run-scoped (registered cuando arranca el Run).
            // Si OnEnable corre antes del bootstrap, _actionRoll queda null. Update()
            // retrieva hasta conseguirlo y se subscribe ahi.
            TrySubscribeToActionRollService();
            Refresh();
        }

        private void Update()
        {
            if (_actionRoll != null) return; // ya subscripto, no-op
            TrySubscribeToActionRollService();
        }

        private void TrySubscribeToActionRollService()
        {
            if (_actionRoll != null) return;
            if (!ServiceLocator.TryGetService<IActionRollService>(out _actionRoll) || _actionRoll == null)
            {
                _actionRoll = null;
                return;
            }
            _onPhase = _ => Refresh();
            _actionRoll.OnPhaseChanged += _onPhase;
            // Recien ahora podemos evaluar si el action roll esta activo — refresh
            // para reflejar el estado actual del service.
            Refresh();
        }

        private void OnDisable()
        {
            if (_actionRoll != null && _onPhase != null)
            {
                _actionRoll.OnPhaseChanged -= _onPhase;
                _onPhase = null;
                _actionRoll = null;
            }
            if (_onPhaseEnter != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseEnter, _onPhaseEnter);
                _onPhaseEnter = null;
            }
            if (_onPhaseExit != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseExit, _onPhaseExit);
                _onPhaseExit = null;
            }
            if (_onDiceFlowStart != null)
            {
                EventManager.UnSubscribe(EventName.OnChainStarted, _onDiceFlowStart);
                EventManager.UnSubscribe(EventName.OnDiceRolled, _onDiceFlowStart);
                _onDiceFlowStart = null;
            }
            if (_onDiceFlowEnd != null)
            {
                EventManager.UnSubscribe(EventName.OnBehaviorExecuted, _onDiceFlowEnd);
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onDiceFlowEnd);
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onDiceFlowEnd);
                _onDiceFlowEnd = null;
            }
            _combatDiceFlowActive = false;
        }

        private void Refresh()
        {
            bool inCombat = ServiceLocator.TryGetService<IPhaseService>(out var phase)
                            && phase != null
                            && phase.CurrentBase == GamePhase.Combat;
            bool actionRollActive = _actionRoll != null && _actionRoll.IsActive;

            // CNF-007 — Combat: visible solo durante el flujo de dados (la zona de chips
            // y la de dados se alternan). Action roll activo (Heal/ForceDoor) también
            // muestra la zona, en cualquier fase.
            ApplyVisible((inCombat && _combatDiceFlowActive) || actionRollActive);
        }

        private void ApplyVisible(bool visible)
        {
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.interactable = visible;
                _group.blocksRaycasts = visible;
            }

            // El PlayerActionButtonsView gestiona Button.interactable según _phase del
            // combat HUD — en exploración no corre y el botón queda gris aunque el
            // CanvasGroup esté interactable. Forzamos el override mientras estemos visibles.
            if (_forceButtonInteractable && _ownButton != null)
            {
                _ownButton.interactable = visible;
            }
        }
    }
}
