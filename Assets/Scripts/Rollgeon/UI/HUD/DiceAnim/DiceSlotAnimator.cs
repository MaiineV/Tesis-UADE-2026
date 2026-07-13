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
            if (plan.Delay > 0f) yield return new WaitForSeconds(plan.Delay);

            IsSpinning = true;
            SpinStarted?.Invoke();

            // Rotación por Custom: los tweens de Quaternion toman el camino corto y
            // no sirven para vueltas completas.
            float totalDegrees = 360f * _settings.SpinTurns;
            if (_rotationTween.isAlive) _rotationTween.Stop();
            _rotationTween = Tween.Custom(0f, totalDegrees, plan.Duration, ease: _settings.SpinEase,
                onValueChange: v =>
                {
                    if (_rect != null) _rect.localEulerAngles = new Vector3(0f, 0f, v);
                });

            int faceRange = DiceAnimChoreographer.PreviewFaceRange(_settings.PreviewFaceMax, finalFace);
            var rng = new System.Random(unchecked(Environment.TickCount * 31 + GetInstanceID()));
            int previewFace = 0;
            int nextTick = 1;
            float elapsed = 0f;
            while (elapsed < plan.Duration)
            {
                if (nextTick <= plan.TickCount && elapsed >= DiceAnimChoreographer.TickTime(
                        nextTick, plan.TickCount, plan.Duration, _settings.SpinDecelerationPower))
                {
                    previewFace = DiceAnimChoreographer.NextPreviewFace(rng, faceRange, previewFace);
                    _view.SetSpinPreviewFace(previewFace);
                    nextTick++;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (_rotationTween.isAlive) _rotationTween.Stop();
            if (_rect != null) _rect.localEulerAngles = Vector3.zero;

            IsSpinning = false;
            _spinRoutine = null;
            _view.ShowFace(finalFace);
            _view.SetHoldInteractable(true);
            FaceRevealed?.Invoke(finalFace);
            onRevealed?.Invoke(finalFace);
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
            if (_rect != null && _initialized) _rect.localEulerAngles = Vector3.zero;
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
            var target = raised ? _basePos + new Vector2(0f, _settings.RaiseOffsetY) : _basePos;
            float seconds = raised ? _settings.RaiseSeconds : _settings.LowerSeconds;
            if (seconds <= 0f || !gameObject.activeInHierarchy)
                _rect.anchoredPosition = target;
            else
                _raiseTween = Tween.UIAnchoredPosition(_rect, target, seconds, _settings.RaiseEase);

            if (raised) DieLocked?.Invoke();
            else DieUnlocked?.Invoke();
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

            if (_moveTween.isAlive) _moveTween.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();
            if (_fadeTween.isAlive) _fadeTween.Stop();

            switch (plan.Kind)
            {
                case DiceOutroKind.Throw:
                    ThrowStarted?.Invoke();
                    _moveTween = Tween.UIAnchoredPosition(_rect, plan.TargetPosition,
                        plan.Duration, _settings.ThrowEase, startDelay: plan.Delay);
                    _scaleTween = Tween.Scale(_rect, _baseScale * plan.EndScale,
                        plan.Duration, _settings.ThrowEase, startDelay: plan.Delay);
                    break;

                case DiceOutroKind.Discard:
                    DieDiscarded?.Invoke();
                    _scaleTween = Tween.Scale(_rect, _baseScale * plan.EndScale,
                        plan.Duration, _settings.DiscardEase, startDelay: plan.Delay);
                    break;
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
