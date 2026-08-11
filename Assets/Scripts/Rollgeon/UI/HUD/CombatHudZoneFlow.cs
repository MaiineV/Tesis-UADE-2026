using Patterns;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// CNF-007 — alterna la zona de chips (acciones) con la zona de dados. Al arrancar un
    /// flujo de dados (chain iniciado / dados tirados) la zona de chips se apaga y el chip
    /// de la acción activa se muda al anchor izquierdo; al resolverse la acción
    /// (<c>OnBehaviorExecuted</c>) todo vuelve a su lugar. La zona de dados hace el
    /// movimiento inverso por su cuenta (<see cref="ActionRollExplorationVisibility"/>
    /// escucha los mismos eventos), así ambas quedan sincronizadas sin acoplarse.
    /// </summary>
    /// <remarks>
    /// Vive en <c>CombatHUDView</c>, que sólo está activo en combate — en exploración no
    /// hay suscripciones. El chip activo se REPARENTEA al anchor mientras dura el roll
    /// (si sólo se moviera, el fade del CanvasGroup de la zona lo ocultaría con el resto).
    /// Con duraciones &lt;= 0 aplica sin tween (testeable en EditMode, como WallOccluder).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Combat Hud Zone Flow")]
    public sealed class CombatHudZoneFlow : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Tooltip("CanvasGroup de la zona de chips (PlayerActionButtonsView).")]
        private CanvasGroup _chipsGroup;

        [SerializeField, Tooltip("View de los chips — de acá sale cuál es el chip seleccionado.")]
        private PlayerActionButtonsView _buttonsView;

        [SerializeField, Tooltip("Anchor (izquierda) donde se muda el chip de la acción activa " +
                 "mientras dura la fase de dados.")]
        private RectTransform _activeChipAnchor;

        [Title("Tuning")]
        [SerializeField, Tooltip("Fade in/out de la zona de chips. <= 0 aplica instantáneo.")]
        private float _fadeSeconds = 0.15f;

        [SerializeField, Tooltip("Viaje del chip activo hacia/desde el anchor. <= 0 aplica instantáneo.")]
        private float _chipMoveSeconds = 0.2f;

        // ---- Runtime state ---------------------------------------------------

        private bool _rolling;
        private Tween _fadeTween;
        private Tween _chipTween;

        private RectTransform _activeChip;
        private Transform _chipOriginalParent;
        private int _chipOriginalSibling;
        private Vector2 _chipOriginalAnchoredPos;
        private Vector2 _chipOriginalAnchorMin;
        private Vector2 _chipOriginalAnchorMax;
        private Vector2 _chipOriginalPivot;

        private EventManager.EventReceiver _onFlowStart;
        private EventManager.EventReceiver _onFlowEnd;
        private EventManager.EventReceiver _onFlowEndForced;

        // El confirm en modo Classic difiere este exit hasta que el outro de dados
        // termina (DiceOutroGate) — los chips no deben volver mientras los dados vuelan.
        private bool _exitDeferred;

        public bool IsRolling => _rolling;

        // ---- Lifecycle ---------------------------------------------------------

        private void OnEnable()
        {
            if (_buttonsView == null) _buttonsView = GetComponentInChildren<PlayerActionButtonsView>(true);
            if (_chipsGroup == null && _buttonsView != null)
                _chipsGroup = _buttonsView.GetComponent<CanvasGroup>();

            _onFlowStart = _ => EnterRolling();
            _onFlowEnd = _ => ExitRolling(force: false);
            // Fin de combate / turno nuevo: restaurar SIEMPRE, aunque haya outro en el aire.
            _onFlowEndForced = _ => ExitRolling(force: true);
            EventManager.Subscribe(EventName.OnChainStarted, _onFlowStart);
            EventManager.Subscribe(EventName.OnDiceRolled, _onFlowStart);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, _onFlowEnd);
            EventManager.Subscribe(EventName.OnCombatEnd, _onFlowEndForced);
            EventManager.Subscribe(EventName.OnTurnStarted, _onFlowEndForced);
            DiceOutroGate.Changed += HandleOutroGateChanged;
            Rollgeon.Feedback.BreakdownUiGate.Changed += HandleOutroGateChanged;
        }

        private void OnDisable()
        {
            DiceOutroGate.Changed -= HandleOutroGateChanged;
            Rollgeon.Feedback.BreakdownUiGate.Changed -= HandleOutroGateChanged;
            _exitDeferred = false;
            if (_onFlowStart != null)
            {
                EventManager.UnSubscribe(EventName.OnChainStarted, _onFlowStart);
                EventManager.UnSubscribe(EventName.OnDiceRolled, _onFlowStart);
                _onFlowStart = null;
            }
            if (_onFlowEnd != null)
            {
                EventManager.UnSubscribe(EventName.OnBehaviorExecuted, _onFlowEnd);
                _onFlowEnd = null;
            }
            if (_onFlowEndForced != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onFlowEndForced);
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onFlowEndForced);
                _onFlowEndForced = null;
            }

            // Cierre del HUD a mitad de un roll: snap-restore inmediato para no dejar
            // el chip huérfano bajo el anchor ni la zona apagada.
            if (_rolling)
            {
                _rolling = false;
                StopTweens();
                RestoreChip(instant: true);
                SetChipsVisible(true, instant: true);
            }
        }

        // ---- Zone flow ---------------------------------------------------------

        private void EnterRolling()
        {
            // Un roll nuevo puede llegar con el exit del confirm anterior todavía
            // diferido (chain phases): ese exit ya no corresponde — seguimos rolling.
            _exitDeferred = false;
            // Idempotente: OnChainStarted y el primer OnDiceRolled llegan seguidos.
            if (_rolling) return;
            _rolling = true;

            MoveSelectedChipToAnchor();
            SetChipsVisible(false, instant: _fadeSeconds <= 0f);
        }

        private void ExitRolling(bool force)
        {
            if (!_rolling) return;
            if (!force && (DiceOutroGate.OutroPending || Rollgeon.Feedback.BreakdownUiGate.Pending))
            {
                // Los dados siguen volando al centro, o el breakdown N×M está sumando —
                // los chips vuelven cuando ambos gates se liberen (HandleOutroGateChanged).
                _exitDeferred = true;
                return;
            }
            _exitDeferred = false;
            _rolling = false;

            RestoreChip(instant: _chipMoveSeconds <= 0f);
            SetChipsVisible(true, instant: _fadeSeconds <= 0f);
        }

        private void HandleOutroGateChanged()
        {
            if (!_exitDeferred || DiceOutroGate.OutroPending
                || Rollgeon.Feedback.BreakdownUiGate.Pending) return;
            ExitRolling(force: true);
        }

        private void SetChipsVisible(bool visible, bool instant)
        {
            if (_chipsGroup == null) return;
            if (_fadeTween.isAlive) _fadeTween.Stop();

            // Interactabilidad se corta ya mismo (sin esperar el fade) — durante el roll
            // no se puede draguear otro chip.
            _chipsGroup.interactable = visible;
            _chipsGroup.blocksRaycasts = visible;

            float target = visible ? 1f : 0f;
            if (instant)
            {
                _chipsGroup.alpha = target;
                return;
            }

            _fadeTween = Tween.Custom(
                startValue: _chipsGroup.alpha,
                endValue: target,
                duration: _fadeSeconds,
                onValueChange: v =>
                {
                    if (_chipsGroup != null) _chipsGroup.alpha = v;
                });
        }

        // ---- Active chip -------------------------------------------------------

        private void MoveSelectedChipToAnchor()
        {
            _activeChip = null;
            if (_buttonsView == null || _activeChipAnchor == null) return;

            int? slot = _buttonsView.SelectedSlot;
            if (slot == null) return;
            var button = _buttonsView.GetButtonAt(slot.Value);
            if (button == null) return;
            if (button.transform is not RectTransform rt) return;

            _activeChip = rt;
            _chipOriginalParent = rt.parent;
            _chipOriginalSibling = rt.GetSiblingIndex();
            _chipOriginalAnchoredPos = rt.anchoredPosition;
            _chipOriginalAnchorMin = rt.anchorMin;
            _chipOriginalAnchorMax = rt.anchorMax;
            _chipOriginalPivot = rt.pivot;

            // Reparent preservando posición mundial, re-anclado al centro del anchor
            // para que el viaje a anchoredPosition=(0,0) lo deje centrado en él.
            rt.SetParent(_activeChipAnchor, worldPositionStays: true);
            var world = rt.position;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = world;

            if (_chipTween.isAlive) _chipTween.Stop();
            if (_chipMoveSeconds <= 0f)
            {
                rt.anchoredPosition = Vector2.zero;
                return;
            }
            _chipTween = Tween.UIAnchoredPosition(rt, Vector2.zero, _chipMoveSeconds);
        }

        private void RestoreChip(bool instant)
        {
            if (_activeChip == null) return;
            var rt = _activeChip;
            _activeChip = null;

            if (_chipTween.isAlive) _chipTween.Stop();

            rt.SetParent(_chipOriginalParent, worldPositionStays: true);
            rt.SetSiblingIndex(_chipOriginalSibling);
            var world = rt.position;
            rt.anchorMin = _chipOriginalAnchorMin;
            rt.anchorMax = _chipOriginalAnchorMax;
            rt.pivot = _chipOriginalPivot;
            rt.position = world;

            if (instant)
            {
                rt.anchoredPosition = _chipOriginalAnchoredPos;
                return;
            }
            _chipTween = Tween.UIAnchoredPosition(rt, _chipOriginalAnchoredPos, _chipMoveSeconds);
        }

        private void StopTweens()
        {
            if (_fadeTween.isAlive) _fadeTween.Stop();
            if (_chipTween.isAlive) _chipTween.Stop();
        }
    }
}
