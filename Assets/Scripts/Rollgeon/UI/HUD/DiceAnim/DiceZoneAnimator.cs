using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Coordina la animación de los slots de dados legacy (modo Classic): spin del
    /// roll, raise del hold y outro del confirm. <see cref="DiceZoneView"/> lo agrega
    /// por código en <c>Bind</c> — si un <c>TryBegin*</c> devuelve <c>false</c> (modo
    /// no Classic, sin slots, duraciones en 0), el caller ejecuta el path instantáneo
    /// legacy, idéntico al comportamiento pre-animación.
    /// </summary>
    public sealed class DiceZoneAnimator : MonoBehaviour
    {
        [Title("Tuning")]
        [SerializeField, Optional]
        [Tooltip("Tuning de animación. Vacío = Resources/Dice/DiceUiAnimationSettings, " +
                 "y si tampoco existe, defaults de código.")]
        private DiceUiAnimationSettingsSO _settings;

        // ---- Hooks para la capa de juice (DiceZoneJuice) ----------------------
        /// <summary>Arrancó una sesión de spin (roll o reroll) — 1 vez por roll.</summary>
        public event Action SpinSessionStarted;
        public event Action ZoneThrowStarted;
        public event Action OutroFinished;
        public event Action AllFacesRevealed;
        /// <summary>(slotIndex, revealOrder) — para el pitch ramp de los reveals.</summary>
        public event Action<int, int> FaceRevealOrdered;

        /// <summary>Spin u outro arrancó/terminó — las views re-gatean sus botones acá.</summary>
        public event Action AnimationStateChanged;

        public bool IsSpinning => _pendingReveals > 0;
        public bool IsOutroPlaying { get; private set; }

        private DiceSlotView[] _views;
        private DiceSlotAnimator[] _slots;
        private RectTransform _rollArea;
        private bool _bound;

        private int _pendingReveals;
        private int _revealOrder;
        private Coroutine _outroRoutine;
        private Action _onOutroFinished;

        // ---- Bind / Unbind -----------------------------------------------------

        public void Bind(DiceSlotView[] views, RectTransform rollArea)
        {
            _views = views;
            _rollArea = rollArea;
            _slots = new DiceSlotAnimator[views?.Length ?? 0];
            var settings = ResolveSettings();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (views[i] == null) continue;
                var go = views[i].gameObject;
                _slots[i] = go.GetComponent<DiceSlotAnimator>();
                if (_slots[i] == null) _slots[i] = go.AddComponent<DiceSlotAnimator>();
                _slots[i].Init(views[i], settings);
            }
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            ResetAll();
            _views = null;
            _slots = null;
            _rollArea = null;
            _bound = false;
        }

        // ---- Spin ----------------------------------------------------------------

        /// <summary>
        /// Arranca el spin de los slots que revelan cara nueva. <c>false</c> ⇒ el
        /// caller muestra las caras instantáneo (modo no Classic o spin apagado).
        /// El <paramref name="onAllRevealed"/> corre cuando el último dado reveló —
        /// ahí recién se refresca combo preview / bloqueos.
        /// </summary>
        public bool TryBeginSpin(IReadOnlyList<bool> willReveal, IReadOnlyList<int> finalFaces, Action onAllRevealed)
        {
            if (!CanAnimate() || willReveal == null || finalFaces == null) return false;

            var t = ResolveSettings().ToTimings();
            var plans = DiceAnimChoreographer.BuildSpinPlans(willReveal, t);
            int spinCount = 0;
            for (int i = 0; i < plans.Length; i++)
                if (plans[i].Spins) spinCount++;
            if (spinCount == 0) return false;

            // El caller (DiceZoneView) cancela el outro pendiente ANTES de escribir el
            // estado del roll nuevo. Si llegamos acá con uno vivo es un misuse: mejor
            // path instantáneo que dos animaciones peleando por los mismos slots.
            if (IsOutroPlaying) return false;

            _pendingReveals = spinCount;
            _revealOrder = 0;
            for (int i = 0; i < plans.Length; i++)
            {
                if (!plans[i].Spins || _slots[i] == null) continue;
                int slotIndex = i;
                int face = slotIndex < finalFaces.Count ? finalFaces[slotIndex] : 0;
                _slots[i].PlaySpin(plans[i], face, _ =>
                {
                    FaceRevealOrdered?.Invoke(slotIndex, _revealOrder++);
                    _pendingReveals--;
                    if (_pendingReveals > 0) return;
                    AllFacesRevealed?.Invoke();
                    onAllRevealed?.Invoke();
                    AnimationStateChanged?.Invoke();
                });
            }
            SpinSessionStarted?.Invoke();
            AnimationStateChanged?.Invoke();
            return true;
        }

        /// <summary>Animator del slot <paramref name="index"/> (para la capa de juice).</summary>
        public DiceSlotAnimator GetSlotAnimator(int index)
            => _slots != null && index >= 0 && index < _slots.Length ? _slots[index] : null;

        public int SlotCount => _slots?.Length ?? 0;

        /// <summary>Cantidad de dados actualmente elevados (holdeados) — para pitch ramps.</summary>
        public int RaisedCount
        {
            get
            {
                if (_slots == null) return 0;
                int count = 0;
                foreach (var s in _slots)
                    if (s != null && s.IsRaised) count++;
                return count;
            }
        }

        public bool IsSlotSpinning(int index)
            => _slots != null && index >= 0 && index < _slots.Length
               && _slots[index] != null && _slots[index].IsSpinning;

        // ---- Raise ---------------------------------------------------------------

        public void SetRaised(int index, bool raised)
        {
            if (!CanAnimate() || _slots == null || index < 0 || index >= _slots.Length) return;
            _slots[index]?.SetRaised(raised);
        }

        // ---- Outro ---------------------------------------------------------------

        /// <summary>
        /// Arranca el outro del confirm: holdeados vuelan al centro de la mesa con
        /// fade, no usados fade + scale-down. Difiere el <paramref name="onFinished"/>
        /// (típicamente <c>ClearAll</c>) hasta que el último tween termina, y mantiene
        /// <see cref="DiceOutroGate"/> activo para que los teardowns de zona esperen.
        /// <c>false</c> ⇒ el caller limpia instantáneo.
        /// </summary>
        public bool TryBeginOutro(IReadOnlyList<bool> held, IReadOnlyList<bool> active, Action onFinished)
        {
            if (!CanAnimate() || active == null || IsOutroPlaying) return false;

            var t = ResolveSettings().ToTimings();
            var positions = new Vector2[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
                positions[i] = _slots[i] != null ? _slots[i].BasePosition : Vector2.zero;

            var plans = DiceAnimChoreographer.BuildOutroPlans(
                held, active, positions, ComputeTableCenter(), t);
            float hold = Mathf.Max(0f, t.OutroHoldSeconds);
            float total = DiceAnimChoreographer.OutroTotalSeconds(plans, hold);
            if (total <= 0f) return false;

            IsOutroPlaying = true;
            _onOutroFinished = onFinished;
            DiceOutroGate.Begin();

            bool anyThrow = false;
            for (int i = 0; i < plans.Length && i < _slots.Length; i++)
            {
                if (plans[i].Kind == DiceOutroKind.Throw) anyThrow = true;
                _slots[i]?.PlayOutro(plans[i]);
            }
            if (anyThrow) ZoneThrowStarted?.Invoke();

            _outroRoutine = StartCoroutine(OutroTimer(total - hold, hold));
            AnimationStateChanged?.Invoke();
            return true;
        }

        private IEnumerator OutroTimer(float landSeconds, float holdSeconds)
        {
            yield return new WaitForSeconds(landSeconds);
            // Momento de ATERRIZAJE: el juice (shake, thud, partículas, hitstop)
            // dispara acá, ANTES del hold — si saliera junto con el teardown, la
            // zona se esconde el mismo frame y nada se alcanza a ver.
            OutroFinished?.Invoke();
            if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);
            _outroRoutine = null;
            CompleteOutro();
        }

        /// <summary>
        /// Corta un outro en curso y ejecuta YA el teardown diferido (nuevo roll llegó
        /// durante el outro de un chain). Sin outro en curso es no-op.
        /// </summary>
        public void CancelOutroAndComplete()
        {
            if (!IsOutroPlaying) return;
            if (_outroRoutine != null)
            {
                StopCoroutine(_outroRoutine);
                _outroRoutine = null;
            }
            CompleteOutro();
        }

        private void CompleteOutro()
        {
            // Flags primero: el onFinished típico es ClearAll, que re-entra por ResetAll.
            // OutroFinished NO se dispara acá — sale en el momento de aterrizaje del
            // timer (un cancel temprano se salta los visuales de landing, correcto).
            IsOutroPlaying = false;
            var onFinished = _onOutroFinished;
            _onOutroFinished = null;

            if (_slots != null)
                foreach (var s in _slots)
                    s?.ResetVisual();

            onFinished?.Invoke();
            DiceOutroGate.End();
            AnimationStateChanged?.Invoke();
        }

        /// <summary>
        /// Abandona toda animación sin ejecutar callbacks diferidos (turn start, unbind:
        /// el caller ya limpia por su cuenta). Libera el gate si quedó colgado.
        /// </summary>
        public void ResetAll()
        {
            if (_outroRoutine != null)
            {
                StopCoroutine(_outroRoutine);
                _outroRoutine = null;
            }
            bool wasAnimating = IsOutroPlaying || _pendingReveals > 0;
            IsOutroPlaying = false;
            _onOutroFinished = null;
            _pendingReveals = 0;
            if (_slots != null)
                foreach (var s in _slots)
                    s?.ResetVisual();
            DiceOutroGate.End();
            if (wasAnimating) AnimationStateChanged?.Invoke();
        }

        private void OnDisable()
        {
            // Teardown de escena/HUD a mitad de un tween: no dejar el gate colgado
            // (dejaría la zona de dados visible para siempre).
            ResetAll();
        }

        // ---- Internals -----------------------------------------------------------

        private bool CanAnimate()
        {
            if (!_bound || _slots == null || _slots.Length == 0) return false;
            // Accesibilidad: reduced motion apaga todo el sistema (path instantáneo).
            if (DiceUiMotionPrefs.ReducedMotion) return false;
            // Solo el modo Classic anima acá — en 2D/3D los presenters de throw ya
            // animan los dados físicamente (patrón RerollCountView.IsGrabRerollMode).
            return ServiceLocator.TryGetService<Rollgeon.Dice.Throw.IDiceThrowService>(out var t)
                   && t != null && t.Mode == Rollgeon.Dice.Throw.DiceThrowMode.Classic;
        }

        /// <summary>
        /// Centro de la RollArea expresado en anchoredPosition del padre de los slots.
        /// anchoredPosition y localPosition difieren en una constante (anchors fijos),
        /// así que la delta local sirve directo sobre anchoredPosition.
        /// </summary>
        private Vector2 ComputeTableCenter()
        {
            RectTransform reference = null;
            for (int i = 0; i < _slots.Length && reference == null; i++)
                if (_slots[i] != null) reference = (RectTransform)_slots[i].transform;
            if (reference == null || _rollArea == null || reference.parent == null)
                return Vector2.zero;

            Vector3 worldCenter = _rollArea.TransformPoint(_rollArea.rect.center);
            Vector3 parentLocal = reference.parent.InverseTransformPoint(worldCenter);
            Vector2 delta = new Vector2(
                parentLocal.x - reference.localPosition.x,
                parentLocal.y - reference.localPosition.y);
            return reference.anchoredPosition + delta;
        }

        private DiceUiAnimationSettingsSO ResolveSettings()
        {
            if (_settings != null) return _settings;
            _settings = Resources.Load<DiceUiAnimationSettingsSO>("Dice/DiceUiAnimationSettings");
            if (_settings == null)
                _settings = ScriptableObject.CreateInstance<DiceUiAnimationSettingsSO>();
            return _settings;
        }
    }
}
