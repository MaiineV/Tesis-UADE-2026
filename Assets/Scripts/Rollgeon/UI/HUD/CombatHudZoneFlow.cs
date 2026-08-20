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

        [Title("Breath / Punch — selección de target")]
        [SerializeField, Tooltip("Escala pico del breath mientras la acción espera target " +
                 "(estado cancelable). <= 1 desactiva la animación.")]
        private float _breathScale = 1.05f;

        [SerializeField, Tooltip("Medio ciclo del breath (ida). <= 0 desactiva la animación.")]
        private float _breathHalfSeconds = 0.6f;

        [SerializeField, Tooltip("Overshoot del punch al confirmar el target (0.12 = +12%). " +
                 "<= 0 lo desactiva.")]
        private float _punchStrength = 0.12f;

        [SerializeField, Tooltip("Duración total del punch. <= 0 lo desactiva.")]
        private float _punchSeconds = 0.25f;

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
        private EventManager.EventReceiver _onSelectionStart;
        private EventManager.EventReceiver _onTargetChanged;

        // Breath = la acción elegida espera un target (cancelable); el chip respira.
        // Punch = el target se confirmó; golpe corto y el chip queda estático.
        // Pending: los eventos de selección disparan DENTRO del click del chip, antes
        // de que PlayerActionButtonsView setee SelectedSlot — el LateUpdate reintenta
        // resolver el botón al frame siguiente.
        private bool _breathing;
        private bool _breathPending;
        private int? _breathSlot;
        private ActionButton _breathButton;
        private ActionButton _punchButton;
        private Tween _breathTween;
        private Tween _punchTween;

        // El confirm en modo Classic difiere este exit hasta que el outro de dados
        // termina (DiceOutroGate) — los chips no deben volver mientras los dados vuelan.
        private bool _exitDeferred;

        public bool IsRolling => _rolling;

        public bool IsBreathing => _breathing;

        // ---- Lifecycle ---------------------------------------------------------

        private void OnEnable()
        {
            if (_buttonsView == null) _buttonsView = GetComponentInChildren<PlayerActionButtonsView>(true);
            if (_chipsGroup == null && _buttonsView != null)
                _chipsGroup = _buttonsView.GetComponent<CanvasGroup>();

            _onFlowStart = _ => HandleFlowStart();
            _onFlowEnd = _ => { StopBreath(); StopPunch(); ExitRolling(force: false); };
            // Fin de combate / turno nuevo: restaurar SIEMPRE, aunque haya outro en el aire.
            _onFlowEndForced = _ => { StopBreath(); StopPunch(); ExitRolling(force: true); };
            _onSelectionStart = _ => StartBreath();
            _onTargetChanged = args => HandleCombatTargetChanged(args);
            EventManager.Subscribe(EventName.OnChainStarted, _onFlowStart);
            EventManager.Subscribe(EventName.OnDiceRolled, _onFlowStart);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, _onFlowEnd);
            EventManager.Subscribe(EventName.OnCombatEnd, _onFlowEndForced);
            EventManager.Subscribe(EventName.OnTurnStarted, _onFlowEndForced);
            EventManager.Subscribe(EventName.OnChainTargetSelectionStarted, _onSelectionStart);
            EventManager.Subscribe(EventName.OnActionSelectionStarted, _onSelectionStart);
            EventManager.Subscribe(EventName.OnCombatTargetChanged, _onTargetChanged);
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
            if (_onSelectionStart != null)
            {
                EventManager.UnSubscribe(EventName.OnChainTargetSelectionStarted, _onSelectionStart);
                EventManager.UnSubscribe(EventName.OnActionSelectionStarted, _onSelectionStart);
                _onSelectionStart = null;
            }
            if (_onTargetChanged != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatTargetChanged, _onTargetChanged);
                _onTargetChanged = null;
            }

            StopBreath();
            StopPunch();

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

        // ---- Breath / Punch ----------------------------------------------------

        private void HandleFlowStart()
        {
            bool wasBreathing = _breathing;
            EnterRolling();
            // El confirm de la fase 0 de un chain llega como OnChainStarted, sin evento
            // de selección propio: el punch cae sobre el chip ya reparenteado al anchor.
            if (wasBreathing) PunchActiveChip();
            // Un pending que no llegó a respirar antes del roll ya no corresponde —
            // sin esto el retry arrancaría un breath sobre el chip en pleno vuelo.
            _breathPending = false;
        }

        private void HandleCombatTargetChanged(object[] args)
        {
            if (!_breathing) return;
            if (args == null || args.Length < 2 || args[1] is not System.Guid target) return;
            if (target == System.Guid.Empty) StopBreath();
            else PunchActiveChip();
        }

        private void LateUpdate()
        {
            // Retry del breath diferido: al frame siguiente del click el SelectedSlot
            // ya está seteado y el botón se puede resolver.
            if (_breathPending && !_breathing)
            {
                StartBreath();
                if (_breathing) _breathPending = false;
            }

            // El cancel pre-roll (click derecho / reselección de otro chip) no emite
            // ningún evento: si el slot seleccionado ya no es el que respiraba, el
            // estado cancelable murió.
            if (!_breathing || _rolling || _buttonsView == null) return;
            if (_buttonsView.SelectedSlot != _breathSlot) StopBreath();
        }

        private void StartBreath()
        {
            StopBreath();
            var button = ResolveBreathTarget();
            if (button == null)
            {
                // Evento en vuelo dentro del click del chip: SelectedSlot todavía no
                // está seteado. El LateUpdate reintenta al frame siguiente.
                _breathPending = true;
                return;
            }

            _breathing = true;
            _breathButton = button;
            _breathSlot = _buttonsView != null ? _buttonsView.SelectedSlot : null;

            // Bajo reduced motion (o en EditMode) el estado breath existe igual — el
            // punch y los cancels lo necesitan — pero sin animación.
            if (DiceUiMotionPrefs.ReducedMotion || !Application.isPlaying
                || _breathScale <= 1f || _breathHalfSeconds <= 0f) return;

            _breathTween = Tween.Custom(1f, _breathScale, _breathHalfSeconds,
                onValueChange: v =>
                {
                    if (_breathButton != null) _breathButton.SetExternalScaleMultiplier(v);
                },
                ease: Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        private void StopBreath()
        {
            _breathing = false;
            _breathPending = false;
            _breathSlot = null;
            if (_breathTween.isAlive) _breathTween.Stop();
            if (_breathButton != null) _breathButton.SetExternalScaleMultiplier(1f);
            _breathButton = null;
        }

        private void StopPunch()
        {
            if (_punchTween.isAlive) _punchTween.Stop();
            if (_punchButton != null) _punchButton.SetExternalScaleMultiplier(1f);
            _punchButton = null;
        }

        private void PunchActiveChip()
        {
            // Tras el reparent el chip vivo es _activeChip — el botón cacheado del
            // breath puede ser el mismo, pero re-resolver cubre el punch post-move.
            var button = _rolling && _activeChip != null
                ? _activeChip.GetComponent<ActionButton>()
                : _breathButton;
            if (button == null) button = _breathButton;
            StopBreath();
            StopPunch();
            if (button == null) return;

            if (DiceUiMotionPrefs.ReducedMotion || !Application.isPlaying
                || _punchStrength <= 0f || _punchSeconds <= 0f) return;

            _punchButton = button;
            _punchTween = Tween.Custom(1f, 1f + _punchStrength, _punchSeconds * 0.5f,
                onValueChange: v =>
                {
                    if (_punchButton != null) _punchButton.SetExternalScaleMultiplier(v);
                },
                ease: Ease.OutQuad, cycles: 2, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        private ActionButton ResolveBreathTarget()
        {
            if (_rolling && _activeChip != null) return _activeChip.GetComponent<ActionButton>();
            if (_buttonsView == null) return null;
            int? slot = _buttonsView.SelectedSlot;
            if (slot == null) return null;
            return _buttonsView.GetButtonAt(slot.Value);
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
