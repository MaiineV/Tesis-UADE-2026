using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Phase;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Gateaje de visibilidad para los views compartidos (DiceZoneView, DamageFormulaView)
    /// que viven en el Canvas raíz. Regla: en <see cref="GamePhase.Combat"/> siempre visible;
    /// en <see cref="GamePhase.Exploration"/> visible solo cuando hay un <see cref="IActionRollService"/>
    /// activo (heal con poción, etc.). Sin esto los slots de dados se ven en exploration
    /// aunque el user no haya iniciado ninguna acción.
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
        }

        private void Refresh()
        {
            bool inCombat = ServiceLocator.TryGetService<IPhaseService>(out var phase)
                            && phase != null
                            && phase.CurrentBase == GamePhase.Combat;
            bool actionRollActive = _actionRoll != null && _actionRoll.IsActive;

            // Combat: siempre visible. Exploration: solo durante action roll.
            ApplyVisible(inCombat || actionRollActive);
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
