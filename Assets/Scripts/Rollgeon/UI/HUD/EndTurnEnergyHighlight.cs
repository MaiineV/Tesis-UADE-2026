using System;
using Patterns;
using PrimeTween;
using Rollgeon.Combat.EnergyLib;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Aviso de "sin energía" sobre el botón End Turn: cuando el jugador queda en 0
    /// durante su turno, el botón hace glow pulsante, un scale-up leve sostenido y un
    /// trazo de dots recorriendo su contorno con espaciado uniforme. Al recuperar
    /// energía — o al terminar el turno / combate — todo vuelve al reposo.
    /// </summary>
    /// <remarks>
    /// Escala el ROOT del view y no el botón: <see cref="UiButtonJuice"/> (MMF) ya
    /// anima la escala del botón hijo y ambos efectos se pisarían. El gating por
    /// turno/tirada replica el de <see cref="EndTurnButtonView"/> — con un roll en el
    /// aire el botón está deshabilitado y resaltarlo sería mentirle al jugador. Los
    /// colores de glow y dots salen de la autoría de sus Images; acá solo se maneja
    /// el alpha del glow y la posición/visibilidad de los dots.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/End Turn Energy Highlight")]
    public sealed class EndTurnEnergyHighlight : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Tooltip("RectTransform que recibe el scale-up sostenido. Debe ser el " +
                 "root EndTurnButtonView — UiButtonJuice ya tweenea la escala del botón hijo.")]
        private RectTransform _scaleTarget;

        [SerializeField, Tooltip("Image del glow detrás del botón. Arranca invisible (alpha 0).")]
        private Image _glowImage;

        [SerializeField, Tooltip("Contenedor de los dots — mismo rect que el botón.")]
        private RectTransform _dotsContainer;

        [SerializeField, Tooltip("Template de dot (Image hija, inactiva). Se clona hasta llegar " +
                 "a la cantidad configurada.")]
        private Image _dotTemplate;

        [Title("Tuning")]
        [SerializeField, MinValue(1), Tooltip("Cantidad de dots recorriendo el contorno.")]
        private int _dotCount = 8;

        [SerializeField, MinValue(0.1f), Tooltip("Segundos por vuelta completa al contorno.")]
        private float _lapSeconds = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha mínimo del pulso del glow.")]
        private float _glowAlphaMin = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha máximo del pulso del glow.")]
        private float _glowAlphaMax = 0.6f;

        [SerializeField, MinValue(0.05f), Tooltip("Medio ciclo del pulso del glow (ida).")]
        private float _glowPulseSeconds = 0.6f;

        [SerializeField, Range(1f, 1.3f), Tooltip("Escala sostenida del root mientras no hay energía.")]
        private float _activeScale = 1.08f;

        [SerializeField, MinValue(0f), Tooltip("Duración del ease de entrada/salida del scale-up.")]
        private float _scaleSeconds = 0.25f;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private bool _isPlayerTurn;
        private bool _rollInFlight;
        // -1 = sin dato todavía: no resaltar hasta la primera lectura real.
        private int _currentEnergy = -1;
        private bool _active;

        private Vector3 _restScale = Vector3.one;
        private Image[] _dots;
        private Tween _dotsTween;
        private Tween _glowTween;
        private Tween _scaleTween;

        public bool IsHighlightActive => _active;

        private void Awake()
        {
            if (_scaleTarget != null) _restScale = _scaleTarget.localScale;
            SetGlowAlpha(0f);
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

            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEnd);
            _bound = true;

            FetchInitialEnergy();
            Reevaluate();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEnd);
            _bound = false;

            _isPlayerTurn = false;
            _rollInFlight = false;
            _currentEnergy = -1;
            _active = false;
            Deactivate(instant: true);
        }

        // ---- Estado ------------------------------------------------------------

        private void Reevaluate()
        {
            bool shouldBeActive = _bound && _isPlayerTurn && !_rollInFlight && _currentEnergy == 0;
            if (shouldBeActive == _active) return;
            _active = shouldBeActive;
            if (_active) Activate();
            else Deactivate(instant: false);
        }

        private void HandleEnergyChanged(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid guid) || guid != _playerGuid) return;
            if (!(args[1] is int current)) return;
            _currentEnergy = current;
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
        /// <c>OnPlayerEnergyChanged</c> corrige — el highlight solo importa mid-combate,
        /// así que no hace falta el retry por frame de <see cref="EnergyChipStackView"/>.
        /// </summary>
        private void FetchInitialEnergy()
        {
            _currentEnergy = -1;
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null) return;
            if (energy.GetMax(_playerGuid) <= 0) return;
            _currentEnergy = energy.GetCurrent(_playerGuid);
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
            _glowTween = Tween.Custom(_glowAlphaMin, _glowAlphaMax, _glowPulseSeconds,
                onValueChange: SetGlowAlpha,
                ease: Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);

            if (_scaleTarget != null)
                _scaleTween = Tween.Scale(_scaleTarget, _restScale * _activeScale, _scaleSeconds,
                    useUnscaledTime: true);

            EnsureDots();
            if (_dots == null) return;
            foreach (var dot in _dots)
                if (dot != null) dot.gameObject.SetActive(true);
            LayoutDots(0f);
            _dotsTween = Tween.Custom(0f, 1f, _lapSeconds,
                onValueChange: LayoutDots,
                ease: Ease.Linear, cycles: -1, useUnscaledTime: true);
        }

        private void Deactivate(bool instant)
        {
            if (_dotsTween.isAlive) _dotsTween.Stop();
            if (_glowTween.isAlive) _glowTween.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();

            SetGlowAlpha(0f);
            if (_dots != null)
                foreach (var dot in _dots)
                    if (dot != null) dot.gameObject.SetActive(false);

            if (_scaleTarget == null) return;
            if (instant || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                || _scaleSeconds <= 0f)
            {
                _scaleTarget.localScale = _restScale;
                return;
            }
            _scaleTween = Tween.Scale(_scaleTarget, _restScale, _scaleSeconds, useUnscaledTime: true);
        }

        private void EnsureDots()
        {
            if (_dots != null || _dotTemplate == null || _dotsContainer == null) return;
            _dots = new Image[_dotCount];
            _dots[0] = _dotTemplate;
            for (int i = 1; i < _dotCount; i++)
                _dots[i] = Instantiate(_dotTemplate, _dotsContainer);

            // Anclados al centro: PointOnPerimeter devuelve posiciones locales
            // relativas al centro del contenedor.
            foreach (var dot in _dots)
            {
                if (dot == null) continue;
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private void LayoutDots(float phase)
        {
            if (_dots == null || _dotsContainer == null) return;
            Vector2 size = _dotsContainer.rect.size;
            for (int i = 0; i < _dots.Length; i++)
            {
                var dot = _dots[i];
                if (dot == null) continue;
                dot.rectTransform.anchoredPosition =
                    RectPerimeterMath.PointOnPerimeter(size, phase + i / (float)_dots.Length);
            }
        }

        private void SetGlowAlpha(float alpha)
        {
            if (_glowImage == null) return;
            var c = _glowImage.color;
            c.a = alpha;
            _glowImage.color = c;
        }
    }
}
