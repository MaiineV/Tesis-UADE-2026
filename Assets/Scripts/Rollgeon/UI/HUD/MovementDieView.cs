using System;
using System.Collections;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Movement.Die;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Dado de Movimiento suelto (§6.6). Vive DETRÁS de la ficha de Mover (hermano previo
    /// en la jerarquía, misma posición y tamaño) y está oculto salvo durante su acción:
    /// al soltar Mover, sube desde atrás de la ficha con fade-in mientras rolea (giro +
    /// caras ciclando), hace un drop-in al llegar a su posición final (encima de la ficha,
    /// alineado con ella), se detiene mostrando la cara y queda visible hasta que el jugador
    /// elige a dónde moverse (o la acción se cancela / termina el combate).
    /// </summary>
    /// <remarks>
    /// Es el <see cref="IMovementDiePresenter"/> del <see cref="IMovementDieService"/>: el
    /// servicio ya conoce la cara, esta view solo la anima y avisa al aterrizar — el rango se
    /// publica recién en ese callback, así el hover preview no lo spoilea. Entidad visual
    /// separada del <see cref="DiceZoneView"/>: no toca la mesa ni sus 5 slots. La animación
    /// es propia (no <c>DiceSlotAnimator</c>: su salto pelea con el rise) y sigue al game
    /// speed.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Movement Die View")]
    public sealed class MovementDieView : MonoBehaviour, IMovementDiePresenter
    {
        private const string LogPrefix = "[MovementDieView] ";

        [Required("Arrastrar el DiceSlotView hijo (mismo prefab de slot que el DiceZoneView).")]
        [SerializeField] private DiceSlotView _slot;

        [Tooltip("Ficha de Mover con la que se alinea: arranca detrás de ella y sube hasta " +
                 "quedar encima, misma X y mismo ancho. Null = usa la posición propia como final.")]
        [SerializeField] private RectTransform _chip;

        [Title("Rise (sube roleando)")]
        [SerializeField, MinValue(0f)] private float _riseSeconds = 0.45f;
        [SerializeField, MinValue(0f), Tooltip("Separación entre la ficha y el dado al aterrizar.")]
        private float _gap = 10f;
        [SerializeField, MinValue(0f), Tooltip("Cuánto sube de más antes del drop-in.")]
        private float _overshoot = 22f;
        [SerializeField, Tooltip("Vueltas completas durante el rise.")]
        private float _spinTurns = 1.5f;
        [SerializeField, MinValue(0.01f), Tooltip("Cadencia del ciclo de caras random durante el rise.")]
        private float _faceTickSeconds = 0.06f;
        [SerializeField, Range(0.3f, 1f)] private float _startScale = 0.75f;

        [Title("Drop-in (cae y se detiene)")]
        [SerializeField, MinValue(0f)] private float _dropSeconds = 0.28f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Aplastamiento al aterrizar (0 = sin squash).")]
        private float _landSquash = 0.18f;

        [Title("Hide")]
        [SerializeField, MinValue(0f)] private float _fadeOutSeconds = 0.15f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private IMovementDieService _service;
        private bool _bound;
        private Action _pendingReveal;
        private Coroutine _routine;
        private System.Random _previewRng;

        // ---- Lifecycle ---------------------------------------------------------

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // nunca tapa la ficha ni el drag
            _group.interactable = false;
            _previewRng = new System.Random();
        }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            if (_rect == null) Awake();

            if (_slot == null)
            {
                Debug.LogWarning(LogPrefix + "_slot no cableado — el dado de Movimiento no se muestra.", this);
                return;
            }

            if (!ServiceLocator.TryGetService<IMovementDieService>(out _service) || _service == null)
            {
                HideInstant();
                return;
            }

            _service.SetPresenter(this);
            _service.OnCleared += HandleCleared;
            _bound = true;
            HideInstant();
        }

        public void Unbind()
        {
            if (_service != null)
            {
                _service.OnCleared -= HandleCleared;
                _service.SetPresenter(null);
            }
            Abort();
            _service = null;
            _bound = false;
        }

        private void OnDestroy() => Unbind();

        // ---- IMovementDiePresenter ---------------------------------------------

        /// <inheritdoc />
        public bool TryPresent(DiceType type, int face, Action onRevealed)
        {
            if (!_bound || _slot == null || !gameObject.activeInHierarchy) return false;

            StopRoutine();
            _pendingReveal = onRevealed;
            _slot.Bind(type);
            _slot.SetSpinRole(DiceShapeRole.SideA);
            _slot.ClearSpinPreview();
            _routine = StartCoroutine(RiseAndDrop(type, face));
            return true;
        }

        /// <inheritdoc />
        public void Abort()
        {
            _pendingReveal = null;
            StopRoutine();
            if (_slot != null) _slot.SetSpinRole(null);
            HideInstant();
        }

        // ---- Animación ---------------------------------------------------------

        private IEnumerator RiseAndDrop(DiceType type, int face)
        {
            float speed = Mathf.Max(0.01f, Rollgeon.Timing.GameSpeedPrefs.Multiplier);
            AlignWithChip(out var startPos, out var endPos);

            _slot.gameObject.SetActive(true);
            _group.alpha = 0f;
            _rect.anchoredPosition = startPos;
            _rect.localScale = Vector3.one * _startScale;
            _rect.localEulerAngles = Vector3.zero;

            // Rise: sube desde atrás de la ficha, fade-in, gira y cicla caras.
            float rise = _riseSeconds / speed;
            float tick = _faceTickSeconds / speed;
            float nextTick = 0f;
            var peak = endPos + new Vector2(0f, _overshoot);
            for (float t = 0f; rise > 0f && t < rise; t += Time.deltaTime)
            {
                float k = EaseOutCubic(t / rise);
                _rect.anchoredPosition = Vector2.LerpUnclamped(startPos, peak, k);
                _group.alpha = Mathf.Clamp01(t / (rise * 0.6f));
                _rect.localScale = Vector3.one * Mathf.Lerp(_startScale, 1f, k);
                _rect.localEulerAngles = new Vector3(0f, 0f, 360f * _spinTurns * k);
                if (t >= nextTick)
                {
                    _slot.SetSpinPreviewFace(_previewRng.Next(1, type.MaxFace() + 1));
                    nextTick = t + tick;
                }
                yield return null;
            }
            _rect.anchoredPosition = peak;
            _group.alpha = 1f;
            _rect.localScale = Vector3.one;
            _rect.localEulerAngles = Vector3.zero;

            // Drop-in: cae del overshoot a la posición final y se detiene con un squash.
            float drop = _dropSeconds / speed;
            for (float t = 0f; drop > 0f && t < drop; t += Time.deltaTime)
            {
                float k = EaseOutBounce(t / drop);
                _rect.anchoredPosition = Vector2.LerpUnclamped(peak, endPos, k);
                float squash = _landSquash * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (1f - k);
                _rect.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                yield return null;
            }
            _rect.anchoredPosition = endPos;
            _rect.localScale = Vector3.one;

            // Aterrizó: cara real, y recién ahora se publica el rango.
            _slot.SetSpinRole(null);
            _slot.ShowFace(face);
            _routine = null;
            var reveal = _pendingReveal;
            _pendingReveal = null;
            reveal?.Invoke();
        }

        private IEnumerator FadeOutAndHide()
        {
            float speed = Mathf.Max(0.01f, Rollgeon.Timing.GameSpeedPrefs.Multiplier);
            float dur = _fadeOutSeconds / speed;
            float from = _group.alpha;
            for (float t = 0f; dur > 0f && t < dur; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Lerp(from, 0f, t / dur);
                yield return null;
            }
            _routine = null;
            HideInstant();
        }

        // La posición final queda alineada con la ficha: misma X, mismo ancho, apoyada
        // encima con _gap. Sin ficha referenciada, la posición autorada es la final y el
        // dado arranca una ficha más abajo.
        private void AlignWithChip(out Vector2 start, out Vector2 end)
        {
            if (_chip != null)
            {
                _rect.anchorMin = _chip.anchorMin;
                _rect.anchorMax = _chip.anchorMax;
                _rect.pivot = _chip.pivot;
                _rect.sizeDelta = _chip.sizeDelta;
                start = _chip.anchoredPosition;
                end = start + new Vector2(0f, _chip.rect.height + _gap);
                return;
            }
            end = _rect.anchoredPosition;
            start = end - new Vector2(0f, _rect.rect.height + _gap);
        }

        private void HandleCleared()
        {
            if (!_slot.gameObject.activeSelf) return;
            StopRoutine();
            _routine = StartCoroutine(FadeOutAndHide());
        }

        private void HideInstant()
        {
            if (_slot != null) _slot.gameObject.SetActive(false);
            if (_group != null) _group.alpha = 0f;
            if (_rect != null)
            {
                _rect.localScale = Vector3.one;
                _rect.localEulerAngles = Vector3.zero;
            }
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        private static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
    }
}
