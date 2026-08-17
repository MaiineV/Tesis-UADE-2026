using System;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Look de chip para <see cref="Button"/>s planos (los de exploración, que no
    /// tienen el state machine de <see cref="ActionButton"/>): base en reposo,
    /// highlight al hover, y cuando el botón está deshabilitado el mismo look de
    /// "no disponible" que los chips de combate usados — sprite dedicado y la ficha
    /// hundida hasta el borde inferior de la pantalla. Tocarla shakea y avisa via
    /// <see cref="OnBlockedPressed"/> para que el view muestre el motivo.
    /// </summary>
    /// <remarks>
    /// Observa <see cref="Button.interactable"/> por polling en <c>LateUpdate</c>:
    /// <see cref="ExplorationActionButtonsView"/> lo togglea desde varios caminos
    /// (refresh, hotkeys, tutorial) y no emite ningún evento al hacerlo.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Chip Button Visual")]
    [RequireComponent(typeof(Button))]
    public class ChipButtonVisual : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [InfoBox("Sprite al hover. Null = sin swap.")]
        [SerializeField]
        private Sprite _highlightSprite;

        [InfoBox("Sprite del chip deshabilitado (mismo look que la ficha usada de " +
                 "combate). Null = cae al highlight con el alpha de abajo, sin hundirse.")]
        [SerializeField]
        private Sprite _usedSprite;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha del fallback sin sprite de usada.")]
        private float _disabledAlpha = 0.5f;

        [SerializeField, MinValue(0f), Tooltip("Velocidad del hundimiento/regreso (px de pantalla por segundo).")]
        private float _sinkSpeed = 900f;

        [SerializeField, Optional, Tooltip("SFX genérico de rechazo al tocar el chip " +
                 "deshabilitado. Null = mudo.")]
        private AudioClip _rejectClip;

        [SerializeField, Range(0f, 1f), Tooltip("Volumen del SFX de rechazo.")]
        private float _rejectVolume = 0.9f;

        /// <summary>Pointer down sobre el chip deshabilitado — el view resuelve y
        /// muestra el motivo. Delegate plano (mismo patrón que ActionButton).</summary>
        public Action<ChipButtonVisual> OnBlockedPressed;

        private static readonly Vector3[] CornersScratch = new Vector3[4];

        private Button _button;
        private Image _image;
        private Sprite _normalSprite;
        private Color _baseColor = Color.white;
        private bool _hovered;
        private bool _lastInteractable;
        private Vector2 _homeAnchoredPos;
        private bool _needsSinkRestore;
        private Tween _rejectShake;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = _button.targetGraphic as Image;
            if (_image != null)
            {
                _normalSprite = _image.sprite;
                _baseColor = _image.color;
            }
            _lastInteractable = _button.interactable;
            if (transform is RectTransform homeRect) _homeAnchoredPos = homeRect.anchoredPosition;
            Apply();
        }

        private void OnDisable()
        {
            // uGUI no dispara PointerExit sobre un GO desactivado.
            _hovered = false;
            if (_rejectShake.isAlive) _rejectShake.Complete();
        }

        private void OnDestroy()
        {
            if (_rejectShake.isAlive) _rejectShake.Stop();
            OnBlockedPressed = null;
        }

        private void LateUpdate()
        {
            if (_button.interactable != _lastInteractable)
            {
                _lastInteractable = _button.interactable;
                Apply();
            }
            UpdateSink();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Apply();
        }

        /// <summary>Tap sobre el chip deshabilitado: shake + aviso del motivo. El
        /// Button no-interactable no filtra pointer events (mismo truco que
        /// <see cref="ActionButton.OnPointerDown"/>).</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button.interactable) return;
            PlayRejectShake();
            OnBlockedPressed?.Invoke(this);
        }

        private void Apply()
        {
            if (_image == null) return;

            bool disabled = !_button.interactable;
            if (disabled && _usedSprite != null)
            {
                _image.sprite = _usedSprite;
                _image.color = _baseColor; // el hundimiento es el cue — sin atenuar
                return;
            }

            if (_highlightSprite != null)
                _image.sprite = disabled || _hovered ? _highlightSprite : _normalSprite;

            var color = _baseColor;
            if (disabled) color.a *= _disabledAlpha;
            _image.color = color;
        }

        // Mismo enforcement por frame que ActionButton: converge al borde inferior
        // y el regreso es a la anchoredPosition de origen (no por delta — las
        // escrituras absolutas de otros sistemas desincronizaban el acumulado).
        // Solo se hunde cuando hay sprite de usada wireado (fallback = look viejo).
        private void UpdateSink()
        {
            bool shouldSink = !_button.interactable && _usedSprite != null;
            bool instant = !Application.isPlaying || DiceUiMotionPrefs.ReducedMotion || _sinkSpeed <= 0f;
            float step = instant ? float.MaxValue : _sinkSpeed * Time.unscaledDeltaTime;

            if (shouldSink)
            {
                _needsSinkRestore = true;
                float centerY = CurrentScreenCenterY();
                if (centerY <= 0f) return;
                transform.position += Vector3.down * Mathf.Min(step, centerY);
            }
            else if (_needsSinkRestore && transform is RectTransform rect)
            {
                rect.anchoredPosition = Vector2.MoveTowards(rect.anchoredPosition, _homeAnchoredPos, step);
                if (rect.anchoredPosition == _homeAnchoredPos) _needsSinkRestore = false;
            }
        }

        private float CurrentScreenCenterY()
        {
            var rect = transform as RectTransform;
            if (rect == null) return 0f;
            rect.GetWorldCorners(CornersScratch);
            return (CornersScratch[0].y + CornersScratch[1].y) * 0.5f;
        }

        private void PlayRejectShake()
        {
            if (_rejectShake.isAlive) return;

            // Antes del gate de ReducedMotion: el sonido es feedback, no movimiento.
            PlayRejectSfx();

            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion) return;
            _rejectShake = Tween.ShakeLocalPosition(transform,
                strength: new Vector3(6f, 0f, 0f), duration: 0.28f, frequency: 22f,
                useUnscaledTime: true);
        }

        // Con ReducedMotion el shake nunca vive y su guard no debounce-a: rate limit
        // propio para que el mashing no ametralle el clip.
        private float _lastRejectSfxAt = float.NegativeInfinity;

        private void PlayRejectSfx()
        {
            if (!Application.isPlaying || _rejectClip == null) return;
            if (Time.unscaledTime - _lastRejectSfxAt < 0.15f) return;
            _lastRejectSfxAt = Time.unscaledTime;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(_rejectClip, _rejectVolume);
        }
    }
}
