using System;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.Menu
{
    /// <summary>
    /// Efectos por botón del menú (main menu y pausa), calcados del video de
    /// referencia: scale + offset X + subrayado + ciclo de color en hover,
    /// punch con shake al click y entrada animada con delay. Los efectos
    /// continuos corren como lerps en <c>Update</c> (siempre persiguiendo un
    /// target, como el original); los one-shot (entrada, shake) van por
    /// PrimeTween. Todo en tiempo unscaled para sobrevivir pausas o hitstops.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Menu/Juicy Menu Button")]
    [RequireComponent(typeof(Button))]
    public sealed class JuicyMenuButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField, Required]
        private TMP_Text _label;

        [SerializeField, Optional, Tooltip("Barra bajo el texto; el ancho lo maneja el código (0 = oculta).")]
        private RectTransform _underline;

        [SerializeField, Required]
        private MenuJuiceSettingsSO _settings;

        /// <summary>Notifica al grupo para dimming y rombos (botón, highlighted).</summary>
        public event Action<JuicyMenuButton, bool> HighlightChanged;

        // Un botón deshabilitado (ej. Continue sin save) no reacciona al hover.
        public bool Highlighted => (_pointerOver || _selected) && _button != null && _button.interactable;
        public TMP_Text Label => _label;

        private RectTransform _rect;
        private Button _button;
        private CanvasGroup _canvasGroup;

        private Vector2 _restAnchoredPos;
        private Vector3 _baseScale;
        private bool _restCaptured;

        private bool _pointerOver;
        private bool _selected;
        private bool _dimmed;
        private bool _punchActive;
        private float _currentScale = 1f;

        /// <summary>Seteado por <see cref="JuicyMenuGroup"/> cuando otro botón tiene el foco.</summary>
        public void SetDimmed(bool dimmed) => _dimmed = dimmed;

        private Color? _colorOverride;

        /// <summary>
        /// Fuerza el color del label (gana al ciclo pastel y al dim) — necesario
        /// para estados como el "armado" de una confirmación destructiva, que de
        /// otro modo el lerp de color por frame pisaría al instante.
        /// </summary>
        public void SetColorOverride(Color color) => _colorOverride = color;

        public void ClearColorOverride() => _colorOverride = null;

        private bool _initialized;

        private void Awake() => EnsureInit();

        // El grupo (componente del GO padre) recibe OnEnable antes del Awake de
        // los hijos cuando ScreenHost reactiva la screen — los entry points
        // públicos deben poder inicializar por su cuenta.
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _rect = (RectTransform)transform;
            _button = GetComponent<Button>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _button.onClick.AddListener(HandleClick);
        }

        private void OnEnable()
        {
            // Capturar una sola vez: OnDisable restaura el reposo, así que en
            // re-enables la pose ya es la original y recapturar sería seguro,
            // pero si algo externo (layout inicial) movió el botón antes del
            // primer enable, la primera captura es la válida.
            if (!_restCaptured)
            {
                _restAnchoredPos = _rect.anchoredPosition;
                _baseScale = transform.localScale;
                _restCaptured = true;
            }

            if (_label != null) _label.color = _settings.TextColor;
        }

        private void OnDisable()
        {
            // Botón escondido a mitad de un efecto: devolver todo al reposo o
            // queda desplazado/achatado al volver (mismo fix que UiButtonJuice).
            Tween.StopAll(onTarget: transform);
            if (_label != null) Tween.StopAll(onTarget: _label.transform);

            _rect.anchoredPosition = _restAnchoredPos;
            transform.localScale = _baseScale;
            if (_label != null)
            {
                _label.transform.localPosition = Vector3.zero;
                _label.color = _settings.TextColor;
            }
            if (_underline != null)
                _underline.sizeDelta = new Vector2(0f, _underline.sizeDelta.y);

            _currentScale = 1f;
            _punchActive = false;
            _dimmed = false;
            _colorOverride = null;

            bool wasHighlighted = Highlighted;
            _pointerOver = false;
            _selected = false;
            if (wasHighlighted) HighlightChanged?.Invoke(this, false);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            UpdateScale(dt);
            UpdateOffset(dt);
            UpdateUnderline(dt);
            UpdateLabelColor(dt);
        }

        private void UpdateScale(float dt)
        {
            float target = Highlighted ? _settings.HoverScale : 1f;

            // El punch vuelve más rápido que el settle del hover (dt*12 vs dt*8
            // en el video); el flag se apaga al llegar cerca del target.
            float speed = _punchActive ? _settings.PunchReturnSpeed : _settings.ScaleLerpSpeed;
            _currentScale = Mathf.Lerp(_currentScale, target, MenuJuiceMath.SmoothLerpFactor(speed, dt));
            if (_punchActive && Mathf.Abs(_currentScale - target) < 0.02f) _punchActive = false;

            transform.localScale = _baseScale * _currentScale;
        }

        private void UpdateOffset(float dt)
        {
            // Solo X: la Y puede estar tweenada por la entrada staggered.
            float targetX = _restAnchoredPos.x + (Highlighted ? _settings.HoverXOffset : 0f);
            var pos = _rect.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, MenuJuiceMath.SmoothLerpFactor(_settings.OffsetLerpSpeed, dt));
            _rect.anchoredPosition = pos;
        }

        private void UpdateUnderline(float dt)
        {
            if (_underline == null || _label == null) return;

            float target = Highlighted
                ? MenuJuiceMath.UnderlineTargetWidth(_label.preferredWidth, _settings.UnderlinePad)
                : 0f;
            var size = _underline.sizeDelta;
            size.x = Mathf.Lerp(size.x, target, MenuJuiceMath.SmoothLerpFactor(_settings.UnderlineLerpSpeed, dt));
            _underline.sizeDelta = size;
        }

        private void UpdateLabelColor(float dt)
        {
            if (_label == null) return;

            // Solo color de vértice — nunca tocar el material del TMP, que es
            // compartido (el outline vive en un preset de material).
            bool interactable = _button == null || _button.interactable;
            Color target;
            if (!interactable)
            {
                // Deshabilitado se lee como dimmed permanente (Continue sin save).
                target = _settings.DimColor;
            }
            else if (_colorOverride.HasValue)
            {
                target = _colorOverride.Value;
            }
            else if (Highlighted)
            {
                target = MenuJuiceMath.PastelCycle(Time.unscaledTime, _settings.ColorCycleSpeed,
                    _settings.TextColor, _settings.AccentColor, _settings.MutedColor);
            }
            else
            {
                target = _dimmed ? _settings.DimColor : _settings.TextColor;
            }
            _label.color = Color.Lerp(_label.color, target,
                MenuJuiceMath.SmoothLerpFactor(_settings.ColorLerpSpeed, dt));
        }

        /// <summary>Deja el botón invisible y sin raycast a la espera de <see cref="PlayEntrance"/>.</summary>
        public void HideForEntrance()
        {
            EnsureInit();
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void PlayEntrance(float delay)
        {
            EnsureInit();
            if (!_restCaptured)
            {
                _restAnchoredPos = _rect.anchoredPosition;
                _baseScale = transform.localScale;
                _restCaptured = true;
            }

            _rect.anchoredPosition = new Vector2(
                _restAnchoredPos.x, _restAnchoredPos.y - _settings.EntranceYOffset);
            Tween.UIAnchoredPositionY(_rect, _restAnchoredPos.y, _settings.EntranceDuration,
                Ease.OutCubic, startDelay: delay, useUnscaledTime: true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                Tween.Alpha(_canvasGroup, 1f, _settings.EntranceDuration,
                        Ease.OutCubic, startDelay: delay, useUnscaledTime: true)
                    .OnComplete(this, self => self._canvasGroup.blocksRaycasts = true);
            }
        }

        private void HandleClick()
        {
            _currentScale = _settings.PunchScale;
            _punchActive = true;

            // El shake va sobre el label y no sobre el root: Update escribe la
            // anchoredPosition del root cada frame y pisaría el tween.
            if (_label != null)
            {
                Tween.StopAll(onTarget: _label.transform);
                _label.transform.localPosition = Vector3.zero;
                Tween.ShakeLocalPosition(_label.transform,
                    strength: new Vector3(_settings.ShakeStrength, _settings.ShakeStrength, 0f),
                    duration: _settings.ShakeDuration, frequency: 25f, useUnscaledTime: true);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => SetPointerOver(true);
        public void OnPointerExit(PointerEventData eventData) => SetPointerOver(false);
        public void OnSelect(BaseEventData eventData) => SetSelected(true);
        public void OnDeselect(BaseEventData eventData) => SetSelected(false);

        private void SetPointerOver(bool over)
        {
            bool before = Highlighted;
            _pointerOver = over;
            if (before != Highlighted) HighlightChanged?.Invoke(this, Highlighted);
        }

        private void SetSelected(bool selected)
        {
            bool before = Highlighted;
            _selected = selected;
            if (before != Highlighted) HighlightChanged?.Invoke(this, Highlighted);
        }
    }
}
