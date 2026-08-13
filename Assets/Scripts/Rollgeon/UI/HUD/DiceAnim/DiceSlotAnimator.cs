using System;
using System.Collections;
using PrimeTween;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Motion de un slot de dado legacy (modo Classic): spin del roll, raise del hold
    /// y outro del confirm. Ejecuta los planes puros de
    /// <see cref="DiceAnimChoreographer"/> con PrimeTween sobre el ROOT del slot —
    /// la capa de juice (Feel) anima solo el hijo Visual, así nunca se pelean.
    /// Lo agrega <see cref="DiceZoneAnimator"/> por código: no requiere prefab.
    /// </summary>
    public sealed class DiceSlotAnimator : MonoBehaviour
    {
        // Hooks para la capa de juice (DiceSlotJuice). Sin tipos de Feel acá.
        public event Action SpinStarted;
        public event Action PreviewTicked;
        public event Action<int> FaceRevealed;
        public event Action DieLocked;
        public event Action DieUnlocked;
        public event Action ThrowStarted;
        public event Action DieDiscarded;

        public bool IsSpinning { get; private set; }
        public bool IsRaised { get; private set; }

        private DiceSlotView _view;
        private DiceUiAnimationSettingsSO _settings;
        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Vector2 _basePos;
        private Vector3 _baseScale;
        private bool _initialized;

        private Coroutine _spinRoutine;
        private Tween _rotationTween;
        private Tween _raiseTween;
        private Tween _moveTween;
        private Tween _scaleTween;
        private Tween _fadeTween;
        private Tween _jumpTween;
        private Tween _bobTween;

        // Sombra opcional (hijo "Shadow" del prefab) que vende el salto del spin.
        private Transform _shadow;
        private Vector3 _shadowBaseScale;

        public void Init(DiceSlotView view, DiceUiAnimationSettingsSO settings)
        {
            _view = view;
            _settings = settings;
            _rect = (RectTransform)transform;
            // El fade necesita CanvasGroup en el root; el prefab puede no tenerlo.
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (!_initialized)
            {
                _basePos = _rect.anchoredPosition;
                _baseScale = _rect.localScale;
                _shadow = transform.Find("Shadow");
                if (_shadow != null)
                {
                    _shadowBaseScale = _shadow.localScale;
                    _shadow.gameObject.SetActive(false);
                }
                _initialized = true;
            }
        }

        /// <summary>Posición de reposo (para computar targets de outro en espacio del padre).</summary>
        public Vector2 BasePosition => _initialized ? _basePos : ((RectTransform)transform).anchoredPosition;

        // ---- Spin --------------------------------------------------------------

        /// <summary>
        /// Gira el sprite en el lugar ciclando caras random y revela la cara real al
        /// final (ahí recién llama a <c>ShowFace</c>). Durante el giro el hold queda
        /// deshabilitado — un dado que todavía "rueda" no puede lockearse.
        /// </summary>
        public void PlaySpin(DiceSpinPlan plan, int finalFace, Action<int> onRevealed)
        {
            StopSpin();
            if (_view == null || _settings == null || !plan.Spins || !gameObject.activeInHierarchy)
            {
                _view?.ShowFace(finalFace);
                onRevealed?.Invoke(finalFace);
                return;
            }
            _view.SetHoldInteractable(false);
            _spinRoutine = StartCoroutine(SpinRoutine(plan, finalFace, onRevealed));
        }

        private IEnumerator SpinRoutine(DiceSpinPlan plan, int finalFace, Action<int> onRevealed)
        {
            // Stagger de arranque entre dados: pacing puro, sigue al game speed.
            if (plan.Delay > 0f)
                yield return new WaitForSeconds(plan.Delay / Rollgeon.Timing.GameSpeedPrefs.Multiplier);

            IsSpinning = true;
            SpinStarted?.Invoke();

            // Rotación por Custom: los tweens de Quaternion toman el camino corto y
            // no sirven para vueltas completas. Con SpinTurns = 0 no se arranca el tween en
            // vez de correrlo de 0 a 0: escribiría localEulerAngles cada frame por dado y
            // mantendría viva la claim de la capa de motion sobre la rotación (ver el
            // contrato en DiceSlotJuice).
            if (_settings.SpinTurns != 0f)
            {
                float totalDegrees = 360f * _settings.SpinTurns;
                if (_rotationTween.isAlive) _rotationTween.Stop();
                _rotationTween = Tween.Custom(0f, totalDegrees, plan.Duration, ease: _settings.SpinEase,
                    onValueChange: v =>
                    {
                        if (_rect != null) _rect.localEulerAngles = new Vector3(0f, 0f, v);
                    });
            }

            // Salto parabólico + sombra que se achica: el dado "rueda en el aire" y
            // aterriza en el reveal (la sombra es un hijo opcional del prefab).
            if (_settings.SpinJumpHeight > 0f)
            {
                if (_jumpTween.isAlive) _jumpTween.Stop();
                if (_shadow != null) _shadow.gameObject.SetActive(true);
                Vector2 jumpBase = _basePos;
                float jumpHeight = _settings.SpinJumpHeight;
                _jumpTween = Tween.Custom(0f, 1f, plan.Duration, onValueChange: v =>
                {
                    float lift = DiceAnimChoreographer.ParabolaOffset(v, jumpHeight);
                    if (_rect != null) _rect.anchoredPosition = new Vector2(jumpBase.x, jumpBase.y + lift);
                    if (_shadow != null)
                    {
                        // A más altura, sombra más chica y tenue (solo escala — el
                        // alpha lo maneja el color del sprite).
                        float factor = 1f - 0.45f * (jumpHeight > 0f ? lift / jumpHeight : 0f);
                        _shadow.localScale = new Vector3(
                            _shadowBaseScale.x * factor, _shadowBaseScale.y * factor, _shadowBaseScale.z);
                    }
                });
            }

            int faceRange = DiceAnimChoreographer.PreviewFaceRange(_settings.PreviewFaceMax, finalFace);
            var rng = new System.Random(unchecked(Environment.TickCount * 31 + GetInstanceID()));
            // Con qué lateral arranca el ciclado. Se tira una vez por spin: dos dados que giran
            // juntos no van en fase, y el resto de la secuencia es determinista.
            int sideSeed = rng.Next(2);
            bool showPreviewFaces = _settings.ShowPreviewFacesDuringSpin;
            // El label arrastra la cara del roll anterior: hay que vaciarlo al arrancar, no en
            // el primer tick, o el número viejo se queda quieto hasta entonces y se lee como
            // resultado.
            if (!showPreviewFaces) _view.ClearSpinPreview();

            int previewFace = 0;
            int nextTick = 1;
            float elapsed = 0f;
            while (elapsed < plan.Duration)
            {
                if (nextTick <= plan.TickCount && elapsed >= DiceAnimChoreographer.TickTime(
                        nextTick, plan.TickCount, plan.Duration, _settings.SpinDecelerationPower))
                {
                    if (showPreviewFaces)
                    {
                        previewFace = DiceAnimChoreographer.NextPreviewFace(rng, faceRange, previewFace);
                        _view.SetSpinPreviewFace(previewFace);
                    }
                    _view.SetSpinRole(DiceAnimChoreographer.SpinRole(nextTick, sideSeed));
                    // Sigue latiendo con el número apagado: el tick ahora también es el cambio
                    // de sprite, y de él cuelga el SFX del ciclado.
                    PreviewTicked?.Invoke();
                    nextTick++;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (_rotationTween.isAlive) _rotationTween.Stop();
            if (_jumpTween.isAlive) _jumpTween.Stop();
            LandFromSpin();

            IsSpinning = false;
            _spinRoutine = null;
            _view.ShowFace(finalFace);
            _view.SetHoldInteractable(true);
            FaceRevealed?.Invoke(finalFace);
            onRevealed?.Invoke(finalFace);
        }

        private void LandFromSpin()
        {
            // Soltar el rol del ciclado devuelve el slot a su sprite de reposo (frontal, hover o
            // selected según corresponda). Es el backstop de corrección: pase lo que pase con el
            // último tick, o si el spin se cancela a mitad, el dado no queda con un lateral.
            _view?.SetSpinRole(null);
            if (_rect != null && _initialized)
            {
                _rect.localEulerAngles = Vector3.zero;
                _rect.anchoredPosition = _basePos;
            }
            if (_shadow != null)
            {
                _shadow.localScale = _shadowBaseScale;
                _shadow.gameObject.SetActive(false);
            }
        }

        private void StopSpin()
        {
            bool wasPending = _spinRoutine != null;
            if (_spinRoutine != null)
            {
                StopCoroutine(_spinRoutine);
                _spinRoutine = null;
            }
            if (_rotationTween.isAlive) _rotationTween.Stop();
            if (_jumpTween.isAlive) _jumpTween.Stop();
            LandFromSpin();
            IsSpinning = false;
            // Un spin cancelado no debe dejar el hold muerto (lo re-deshabilita el
            // próximo PlaySpin si corresponde).
            if (wasPending) _view?.SetHoldInteractable(true);
        }

        // ---- Raise (hold) ------------------------------------------------------

        public void SetRaised(bool raised)
        {
            if (IsRaised == raised) return;
            IsRaised = raised;
            if (_rect == null || _settings == null) return;

            if (_raiseTween.isAlive) _raiseTween.Stop();
            StopBob();
            var target = raised ? _basePos + new Vector2(0f, _settings.RaiseOffsetY) : _basePos;
            float seconds = raised ? _settings.RaiseSeconds : _settings.LowerSeconds;
            if (seconds <= 0f || !gameObject.activeInHierarchy)
            {
                _rect.anchoredPosition = target;
                if (raised) StartBob(target);
            }
            else
            {
                _raiseTween = Tween.UIAnchoredPosition(_rect, target, seconds, _settings.RaiseEase)
                    .OnComplete(() =>
                    {
                        // El bob arranca recién cuando el raise asentó — y solo si el
                        // dado sigue holdeado (pudo soltarse durante el tween).
                        if (IsRaised) StartBob(target);
                    });
            }

            if (raised) DieLocked?.Invoke();
            else DieUnlocked?.Invoke();
        }

        // Bob flotante del dado holdeado: oscilación senoidal suave alrededor de la
        // posición elevada — refuerza la lectura de "apartado de la mesa".
        private void StartBob(Vector2 center)
        {
            if (_settings == null || _settings.HoldBobAmplitude <= 0f || _settings.HoldBobSeconds <= 0f) return;
            if (!gameObject.activeInHierarchy) return;
            StopBob();
            float amplitude = _settings.HoldBobAmplitude;
            _bobTween = Tween.Custom(0f, Mathf.PI * 2f, _settings.HoldBobSeconds,
                cycles: -1, cycleMode: CycleMode.Restart, ease: Ease.Linear,
                onValueChange: v =>
                {
                    if (_rect != null)
                        _rect.anchoredPosition = new Vector2(center.x, center.y + Mathf.Sin(v) * amplitude);
                });
        }

        private void StopBob()
        {
            if (_bobTween.isAlive) _bobTween.Stop();
        }

        // ---- Outro -------------------------------------------------------------

        /// <summary>
        /// Ejecuta el plan de outro (Throw al centro con fade, o Discard con fade +
        /// scale-down). No limpia el slot: el <see cref="DiceZoneAnimator"/> hace el
        /// ClearAll cuando TODOS los planes terminan.
        /// </summary>
        public void PlayOutro(in DiceOutroPlan plan)
        {
            if (_rect == null || _settings == null || plan.Kind == DiceOutroKind.Skip) return;

            StopBob();
            if (_moveTween.isAlive) _moveTween.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();
            if (_fadeTween.isAlive) _fadeTween.Stop();
            if (_rotationTween.isAlive) _rotationTween.Stop();

            switch (plan.Kind)
            {
                case DiceOutroKind.Throw:
                {
                    ThrowStarted?.Invoke();
                    // Vuelo en arco: lerp al centro + parábola vertical, con rotación
                    // en el aire — el "lanzarse a la mesa" del brief.
                    Vector2 from = _rect.anchoredPosition;
                    Vector2 to = plan.TargetPosition;
                    float arc = _settings.ThrowArcHeight;
                    _moveTween = Tween.Custom(0f, 1f, plan.Duration, ease: _settings.ThrowEase,
                        startDelay: plan.Delay, onValueChange: v =>
                        {
                            if (_rect == null) return;
                            var p = Vector2.LerpUnclamped(from, to, v);
                            p.y += DiceAnimChoreographer.ParabolaOffset(v, arc);
                            _rect.anchoredPosition = p;
                        });
                    if (_settings.ThrowSpinDegrees != 0f)
                        _rotationTween = Tween.Custom(0f, _settings.ThrowSpinDegrees, plan.Duration,
                            ease: Ease.Linear, startDelay: plan.Delay, onValueChange: v =>
                            {
                                if (_rect != null) _rect.localEulerAngles = new Vector3(0f, 0f, v);
                            });
                    _scaleTween = Tween.Scale(_rect, _baseScale * plan.EndScale,
                        plan.Duration, _settings.ThrowEase, startDelay: plan.Delay);
                    break;
                }

                case DiceOutroKind.Discard:
                {
                    DieDiscarded?.Invoke();
                    // Gravedad fake: los descartados caen rotando mientras se apagan.
                    if (_settings.DiscardFallDistance > 0f)
                        _moveTween = Tween.UIAnchoredPosition(_rect,
                            plan.TargetPosition + Vector2.down * _settings.DiscardFallDistance,
                            plan.Duration, _settings.DiscardEase, startDelay: plan.Delay);
                    if (_settings.DiscardSpinDegrees != 0f)
                        _rotationTween = Tween.Custom(0f, _settings.DiscardSpinDegrees, plan.Duration,
                            ease: _settings.DiscardEase, startDelay: plan.Delay, onValueChange: v =>
                            {
                                if (_rect != null) _rect.localEulerAngles = new Vector3(0f, 0f, v);
                            });
                    _scaleTween = Tween.Scale(_rect, _baseScale * plan.EndScale,
                        plan.Duration, _settings.DiscardEase, startDelay: plan.Delay);
                    break;
                }
            }

            if (_canvasGroup != null && plan.FadeDuration > 0f)
                _fadeTween = Tween.Alpha(_canvasGroup, 0f, plan.FadeDuration,
                    Ease.InQuad, startDelay: plan.Delay + plan.FadeDelay);
        }

        // ---- Reset -------------------------------------------------------------

        /// <summary>Frena todo y devuelve el slot a su estado visual de reposo.</summary>
        public void ResetVisual()
        {
            StopAll();
            if (_rect == null || !_initialized) return;
            _rect.anchoredPosition = _basePos;
            _rect.localScale = _baseScale;
            _rect.localEulerAngles = Vector3.zero;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            IsRaised = false;
        }

        public void StopAll()
        {
            StopSpin();
            StopBob();
            if (_raiseTween.isAlive) _raiseTween.Stop();
            if (_moveTween.isAlive) _moveTween.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();
            if (_fadeTween.isAlive) _fadeTween.Stop();
        }

        private void OnDisable()
        {
            // ClearAll hace SetActive(false): los tweens vivos sobre un RectTransform
            // inactivo seguirían escribiendo al reactivarlo.
            StopAll();
        }
    }
}
