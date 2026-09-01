using System;
using System.Collections;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Movement.Die;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Dado de Movimiento suelto (§6.6). Vive DETRÁS de la ficha de Mover (hermano previo
    /// en la jerarquía, misma posición y tamaño) y está oculto salvo durante su acción:
    /// al soltar Mover, sube desde atrás de la ficha con fade-in mientras rolea, hace un
    /// drop-in al llegar a su posición final (encima de la ficha, alineado con ella), se
    /// detiene mostrando la cara y queda visible hasta que el jugador elige a dónde moverse
    /// (o la acción se cancela / termina el combate).
    /// </summary>
    /// <remarks>
    /// El "roleo" es el mismo de los dados de la mesa (<see cref="DiceSlotAnimator"/>): NO
    /// rota el transform — cicla las siluetas Front/SideA/SideB del
    /// <see cref="DiceShapeCatalogSO"/> con ticks que desaceleran, leyendo el mismo
    /// <see cref="DiceUiAnimationSettingsSO"/> de Resources (tick, desaceleración, preview de
    /// caras). Lo que sí es propio es el recorrido (rise + drop-in), que no encaja con el
    /// salto en el lugar del animator de la mesa. Es el <see cref="IMovementDiePresenter"/>
    /// del <see cref="IMovementDieService"/>: el servicio ya conoce la cara, esta view solo
    /// la anima y avisa al aterrizar — el rango se publica recién ahí. No toca la mesa ni
    /// sus 5 slots.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Movement Die View")]
    public sealed class MovementDieView : MonoBehaviour, IMovementDiePresenter
    {
        private const string LogPrefix = "[MovementDieView] ";
        private const string SettingsResourcePath = "Dice/DiceUiAnimationSettings";

        [Required("Arrastrar el DiceSlotView hijo (mismo prefab de slot que el DiceZoneView).")]
        [SerializeField] private DiceSlotView _slot;

        [Tooltip("Ficha de Mover con la que se alinea: arranca detrás de ella y sube hasta " +
                 "quedar encima, misma X y mismo ancho. Null = usa la posición propia como final.")]
        [SerializeField] private RectTransform _chip;

        [SerializeField, Tooltip("Opcional: override del tuning de spin (tick, desaceleración, " +
                                 "preview de caras). Null = Resources/" + SettingsResourcePath + ".")]
        private DiceUiAnimationSettingsSO _animSettings;

        [SerializeField, Tooltip("Opcional: label del bonus de MoveRange (Botas/Guantelete). " +
                                 "Se muestra como \"+N\"/\"-N\" junto a la cara al aterrizar; " +
                                 "oculto con bonus 0 o sin referencia.")]
        private TextMeshProUGUI _bonusLabel;

        [Title("Rise (sube roleando)")]
        [SerializeField, MinValue(0f)] private float _riseSeconds = 0.45f;
        [SerializeField, MinValue(0f), Tooltip("Separación entre la ficha y el dado al aterrizar.")]
        private float _gap = 10f;
        [SerializeField, MinValue(0f), Tooltip("Cuánto sube de más antes del drop-in.")]
        private float _overshoot = 22f;
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
        private int _pendingBonus;
        private Coroutine _routine;

        // ---- Lifecycle ---------------------------------------------------------

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // nunca tapa la ficha ni el drag
            _group.interactable = false;
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
        public bool TryPresent(DiceType type, int face, int rangeBonus, Action onRevealed)
        {
            if (!_bound || _slot == null || !gameObject.activeInHierarchy) return false;

            StopRoutine();
            _pendingReveal = onRevealed;
            _pendingBonus = rangeBonus;
            if (_bonusLabel != null) _bonusLabel.gameObject.SetActive(false);
            _slot.Bind(type);
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
            var settings = ResolveSettings();
            AlignWithChip(out var startPos, out var endPos);

            _slot.gameObject.SetActive(true);
            _group.alpha = 0f;
            _rect.anchoredPosition = startPos;
            _rect.localScale = Vector3.one * _startScale;
            _rect.localEulerAngles = Vector3.zero;

            // Roleo idéntico al de la mesa: ciclado de siluetas con ticks que desaceleran
            // a lo largo de TODO el recorrido (rise + drop), sin rotar el transform. El
            // número queda apagado salvo que el tuning pida preview de caras.
            float rise = _riseSeconds / speed;
            float drop = _dropSeconds / speed;
            float total = rise + drop;
            int tickCount = DiceAnimChoreographer.TickCount(total, settings.SpinTickSeconds / speed);
            var rng = new System.Random(unchecked(Environment.TickCount * 31 + GetInstanceID()));
            int sideSeed = rng.Next(2);
            bool showPreviewFaces = settings.ShowPreviewFacesDuringSpin;
            int faceRange = DiceAnimChoreographer.PreviewFaceRange(Mathf.Min(settings.PreviewFaceMax, type.MaxFace()), face);
            int previewFace = 0;
            int nextTick = 1;
            _slot.ClearSpinPreview();
            _slot.SetSpinRole(DiceAnimChoreographer.SpinRole(0, sideSeed));

            void Tick(float elapsed)
            {
                if (nextTick > tickCount) return;
                if (elapsed < DiceAnimChoreographer.TickTime(nextTick, tickCount, total, settings.SpinDecelerationPower))
                    return;
                if (showPreviewFaces)
                {
                    previewFace = DiceAnimChoreographer.NextPreviewFace(rng, faceRange, previewFace);
                    _slot.SetSpinPreviewFace(previewFace);
                }
                _slot.SetSpinRole(DiceAnimChoreographer.SpinRole(nextTick, sideSeed));
                nextTick++;
            }

            // Rise: sube desde atrás de la ficha con fade-in y crece hasta su tamaño.
            var peak = endPos + new Vector2(0f, _overshoot);
            float t = 0f;
            for (; rise > 0f && t < rise; t += Time.deltaTime)
            {
                float k = EaseOutCubic(t / rise);
                _rect.anchoredPosition = Vector2.LerpUnclamped(startPos, peak, k);
                _group.alpha = Mathf.Clamp01(t / (rise * 0.6f));
                _rect.localScale = Vector3.one * Mathf.Lerp(_startScale, 1f, k);
                Tick(t);
                yield return null;
            }
            _rect.anchoredPosition = peak;
            _group.alpha = 1f;
            _rect.localScale = Vector3.one;

            // Drop-in: cae del overshoot a la posición final y se detiene con un squash.
            for (float d = 0f; drop > 0f && d < drop; d += Time.deltaTime)
            {
                float k = EaseOutBounce(d / drop);
                _rect.anchoredPosition = Vector2.LerpUnclamped(peak, endPos, k);
                float squash = _landSquash * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (1f - k);
                _rect.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                Tick(rise + d);
                yield return null;
            }
            _rect.anchoredPosition = endPos;
            _rect.localScale = Vector3.one;

            // Aterrizó: silueta frontal, cara real, y recién ahora se publica el rango.
            _slot.SetSpinRole(null);
            _slot.ShowFace(face);
            ShowBonus(_pendingBonus);
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
            if (_slot == null || !_slot.gameObject.activeSelf) return;
            StopRoutine();
            _routine = StartCoroutine(FadeOutAndHide());
        }

        // El "+N" aparece recién con la cara firme: durante el spin sería ruido y
        // spoilearía que hay modificador antes de saber sobre qué cara aplica.
        private void ShowBonus(int bonus)
        {
            if (_bonusLabel == null) return;
            if (bonus == 0)
            {
                _bonusLabel.gameObject.SetActive(false);
                return;
            }
            _bonusLabel.text = bonus > 0 ? "+" + bonus : bonus.ToString();
            _bonusLabel.gameObject.SetActive(true);
        }

        private void HideInstant()
        {
            if (_slot != null) _slot.gameObject.SetActive(false);
            if (_bonusLabel != null) _bonusLabel.gameObject.SetActive(false);
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

        private DiceUiAnimationSettingsSO ResolveSettings()
        {
            if (_animSettings != null) return _animSettings;
            _animSettings = Resources.Load<DiceUiAnimationSettingsSO>(SettingsResourcePath);
            if (_animSettings == null)
                _animSettings = ScriptableObject.CreateInstance<DiceUiAnimationSettingsSO>();
            return _animSettings;
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
