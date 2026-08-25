using System;
using Patterns;
using PrimeTween;
using Rollgeon.Combat.Rolls;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Aviso de "sin rolls" sobre el botón End Turn: cuando el pool queda en 0
    /// durante el turno del jugador, el botón respira — glow radial con pulso de
    /// alpha y scale yoyo del root, ambos con el mismo período ("terminá el turno
    /// para recuperar rolls"). Al recuperar rolls — o al terminar el turno /
    /// combate — todo vuelve al reposo.
    /// </summary>
    /// <remarks>
    /// Escala el ROOT del view y no el botón: <see cref="UiButtonJuice"/> (MMF) ya
    /// anima la escala del botón hijo y ambos efectos se pisarían. El gating por
    /// turno/tirada replica el de <see cref="EndTurnButtonView"/> — con un roll en el
    /// aire el botón está deshabilitado y resaltarlo sería mentirle al jugador. El
    /// color del glow sale de la autoría de su Image; acá solo se maneja el alpha.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/End Turn Rolls Highlight")]
    public sealed class EndTurnRollsHighlight : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Tooltip("RectTransform que recibe el scale yoyo del breath. Debe ser el " +
                 "root EndTurnButtonView — UiButtonJuice ya tweenea la escala del botón hijo.")]
        private RectTransform _scaleTarget;

        [SerializeField, Tooltip("Image del glow radial detrás del botón. Arranca invisible (alpha 0).")]
        private Image _glowImage;

        [SerializeField, Tooltip("RectTransform del botón juiceado. JuicyMenuButton lo escala y " +
                 "desplaza en hover/press — el glow espeja esa pose para acompañarlo.")]
        private RectTransform _followButton;

        [Title("Tuning")]
        [SerializeField, Range(0f, 1f), Tooltip("Alpha mínimo del pulso del glow.")]
        private float _glowAlphaMin = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha máximo del pulso del glow.")]
        private float _glowAlphaMax = 0.6f;

        [SerializeField, Range(1f, 1.3f), Tooltip("Pico del scale yoyo del root durante el breath.")]
        private float _activeScale = 1.08f;

        [SerializeField, MinValue(0.05f), Tooltip("Medio ciclo del breath (ida). Comparten período " +
                 "el pulso de alpha del glow y el scale yoyo, así respiran juntos.")]
        private float _breathSeconds = 0.8f;

        [SerializeField, MinValue(0f), Tooltip("Duración del ease de vuelta al reposo al apagarse.")]
        private float _scaleSeconds = 0.25f;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private bool _isPlayerTurn;
        private bool _rollInFlight;
        // -1 = sin dato todavía: no resaltar hasta la primera lectura real.
        private int _currentRolls = -1;
        private bool _active;

        private Vector3 _restScale = Vector3.one;
        private Tween _glowTween;
        private Tween _scaleTween;

        private bool _followCaptured;
        private Vector3 _followRestScale = Vector3.one;
        private Vector2 _followRestPos;
        private Vector3 _glowRestScale = Vector3.one;
        private Vector2 _glowRestPos;

        public bool IsHighlightActive => _active;

        private void Awake()
        {
            if (_scaleTarget != null) _restScale = _scaleTarget.localScale;
            CaptureFollowRest();
            SetGlowAlpha(0f);
        }

        private void LateUpdate()
        {
            // Después de los Update de JuicyMenuButton, que escribe la escala y el
            // offset del botón cada frame — así el glow lo sigue sin lag.
            if (_active) MirrorButtonPose();
        }

        private void OnDisable()
        {
            _active = false;
            Deactivate(instant: true);
        }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEnd);
            _bound = true;

            CaptureFollowRest();
            FetchInitialRolls();
            Reevaluate();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEnd);
            _bound = false;

            _isPlayerTurn = false;
            _rollInFlight = false;
            _currentRolls = -1;
            _active = false;
            Deactivate(instant: true);
        }

        // ---- Estado ------------------------------------------------------------

        private void Reevaluate()
        {
            bool shouldBeActive = _bound && _isPlayerTurn && !_rollInFlight && _currentRolls == 0;
            if (shouldBeActive == _active) return;
            _active = shouldBeActive;
            if (_active) Activate();
            else Deactivate(instant: false);
        }

        private void HandleRollsChanged(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid guid) || guid != _playerGuid) return;
            if (!(args[1] is int current)) return;
            _currentRolls = current;
            Reevaluate();
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _isPlayerTurn = true;
            _rollInFlight = false;
            Reevaluate();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _isPlayerTurn = false;
            Reevaluate();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _rollInFlight = true;
            Reevaluate();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (!IsForPlayer(args)) return;
            _rollInFlight = false;
            Reevaluate();
        }

        private void HandleCombatEnd(params object[] args)
        {
            _isPlayerTurn = false;
            _rollInFlight = false;
            Reevaluate();
        }

        private bool IsForPlayer(object[] args)
            => args != null && args.Length >= 1 && args[0] is Guid guid && guid == _playerGuid;

        /// <summary>
        /// Estado inicial silencioso: si el servicio/ruleset aún no existen, el primer
        /// <c>OnPlayerRollsChanged</c> corrige — el highlight solo importa mid-combate,
        /// así que no hace falta el retry por frame de <see cref="RollCupView"/>.
        /// </summary>
        private void FetchInitialRolls()
        {
            _currentRolls = -1;
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null) return;
            if (!rolls.IsCombatActive) return;
            _currentRolls = rolls.GetCurrent(_playerGuid);
        }

        // ---- Efectos -----------------------------------------------------------

        private void Activate()
        {
            if (DiceAnim.DiceUiMotionPrefs.ReducedMotion || !Application.isPlaying)
            {
                // Sin movimiento el aviso sigue existiendo: glow fijo al alpha máximo.
                SetGlowAlpha(_glowAlphaMax);
                return;
            }

            SetGlowAlpha(_glowAlphaMin);
            _glowTween = Tween.Custom(_glowAlphaMin, _glowAlphaMax, _breathSeconds,
                onValueChange: SetGlowAlpha,
                ease: Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);

            if (_scaleTarget != null)
            {
                // Rebase al reposo por si el ease de salida quedó a mitad de camino:
                // el yoyo respira desde el valor actual y arrancar corrido lo sesga.
                _scaleTarget.localScale = _restScale;
                _scaleTween = Tween.Scale(_scaleTarget, _restScale * _activeScale, _breathSeconds,
                    Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                    useUnscaledTime: true);
            }
        }

        private void Deactivate(bool instant)
        {
            if (_glowTween.isAlive) _glowTween.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();

            SetGlowAlpha(0f);
            RestoreFollowPose();

            if (_scaleTarget == null) return;
            if (instant || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                || _scaleSeconds <= 0f)
            {
                _scaleTarget.localScale = _restScale;
                return;
            }
            _scaleTween = Tween.Scale(_scaleTarget, _restScale, _scaleSeconds, useUnscaledTime: true);
        }

        private void SetGlowAlpha(float alpha)
        {
            if (_glowImage == null) return;
            var c = _glowImage.color;
            c.a = alpha;
            _glowImage.color = c;
        }

        // ---- Espejo del hover del botón -----------------------------------------

        /// <summary>
        /// Reposo del botón y del glow. Se captura una sola vez y solo con el
        /// botón quieto (Awake / Bind): capturar en el primer frame activo podría
        /// pescar un hover a medias y correr el reposo para siempre.
        /// </summary>
        private void CaptureFollowRest()
        {
            if (_followCaptured || _followButton == null) return;
            _followRestScale = _followButton.localScale;
            _followRestPos = _followButton.anchoredPosition;
            if (_glowImage != null)
            {
                _glowRestScale = _glowImage.rectTransform.localScale;
                _glowRestPos = _glowImage.rectTransform.anchoredPosition;
            }
            _followCaptured = true;
        }

        private void MirrorButtonPose()
        {
            if (!_followCaptured || _followButton == null) return;
            float ratio = _followRestScale.x != 0f
                ? _followButton.localScale.x / _followRestScale.x
                : 1f;
            Vector2 delta = _followButton.anchoredPosition - _followRestPos;
            if (_glowImage != null)
                ApplyPose(_glowImage.rectTransform, _glowRestScale, _glowRestPos, ratio, delta);
        }

        private void RestoreFollowPose()
        {
            if (!_followCaptured) return;
            if (_glowImage != null)
                ApplyPose(_glowImage.rectTransform, _glowRestScale, _glowRestPos, 1f, Vector2.zero);
        }

        private static void ApplyPose(RectTransform target, Vector3 restScale, Vector2 restPos,
            float scaleRatio, Vector2 posDelta)
        {
            target.localScale = restScale * scaleRatio;
            target.anchoredPosition = restPos + posDelta;
        }
    }
}
