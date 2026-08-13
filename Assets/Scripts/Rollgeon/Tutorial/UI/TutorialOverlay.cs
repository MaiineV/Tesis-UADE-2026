using System;
using Patterns;
using PrimeTween;
using Rollgeon.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>
    /// Canvas ScreenSpace-Overlay persistente del tutorial (patrón
    /// <c>LoadingScreen</c>/<c>PersistentUiOverlay</c>: armado por código,
    /// <c>DontDestroyOnLoad</c>, NO pasa por el <c>ScreenManager</c> — un Push
    /// ocultaría el HUD de abajo). Dim con recorte circular por shader
    /// (Rollgeon/UI/TutorialDim), flecha rotada hacia el recorte y popup TMP en
    /// el cuadrante opuesto.
    /// <para>
    /// Input: todo con <c>raycastTarget=false</c> — los clicks de mundo
    /// (TileClickHandler gatea con <c>IsPointerOverGameObject</c>) y los botones
    /// del HUD siguen funcionando. Excepción: <see cref="TutorialInputPolicy.BlockUntilContinue"/>
    /// activa el raycast del dim y un click-catcher que dispara el callback.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TutorialOverlay : MonoBehaviour, ITutorialOverlayService
    {
        private static readonly int DimColorId = Shader.PropertyToID("_DimColor");
        private static readonly int CutoutCenterId = Shader.PropertyToID("_CutoutCenter");
        private static readonly int CutoutRadiusId = Shader.PropertyToID("_CutoutRadius");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int ShowId = Shader.PropertyToID("_Show");

        private static TutorialOverlay _instance;

        private TutorialOverlaySettingsSO _settings;
        private Material _dimMaterial;
        private Image _dim;
        private Image _arrow;
        private RectTransform _popupRoot;
        private Image _popupBackground;
        private TextMeshProUGUI _popupText;
        private TextMeshProUGUI _popupFooter;

        private TutorialStepDisplayRequest _request;
        private Action _onContinue;
        private Canvas _canvas;
        private int _anchorQuadrant;
        private int _popupSide;
        private float _arrowBobPhase;
        private Tween _showTween;

        // Anti-baile con la cámara en movimiento (zoom/rotación): si el anchor no
        // resuelve por un frame se usa el último conocido durante una gracia corta
        // (el fallback instantáneo al centro hacía aparecer/desaparecer el popup),
        // y el centro del popup se suaviza en vez de teletransportarse.
        private const float AnchorLossGraceSeconds = 0.35f;
        private const float PopupSmoothing = 14f;
        private Vector2 _lastAnchorPos;
        private bool _hadAnchor;
        private float _anchorLostAt = -1f;
        private Vector2 _smoothedPopupCenter;
        private bool _popupCenterInitialized;

        public bool IsVisible { get; private set; }

        /// <summary>Crea el GameObject persistente la primera vez que se llama.</summary>
        public static TutorialOverlay Create(TutorialOverlaySettingsSO settings)
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[TutorialOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TutorialOverlay>();
            _instance._settings = settings;
            _instance.BuildHierarchy();
            go.SetActive(false);

            SceneManager.activeSceneChanged += _instance.OnActiveSceneChanged;
            return _instance;
        }

        private void OnDestroy()
        {
            SetHotkeysSuppressed(false);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_dimMaterial != null) Destroy(_dimMaterial);
            if (_instance == this) _instance = null;
        }

        // Un load de escena a mitad de paso (muerte, quit al menú) no debe dejar
        // un dim colgado — y el RectTransform target muere con el HUD viejo.
        private void OnActiveSceneChanged(Scene from, Scene to) => HideInstant();

        // ==================================================================
        // Build
        // ==================================================================

        private void BuildHierarchy()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _settings.SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _settings.ReferenceResolution;
            scaler.matchWidthOrHeight = _settings.MatchWidthOrHeight;

            // Solo usado por BlockUntilContinue — inerte mientras nada es raycast target.
            gameObject.AddComponent<GraphicRaycaster>();

            // --- Dim (full-stretch, shader con recorte) ---
            _dim = CreateImage("Dim", transform);
            var dimRect = _dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            // Instancia propia: NO mutar el asset compartido del material.
            _dimMaterial = new Material(_settings.DimMaterial);
            _dim.material = _dimMaterial;
            _dim.gameObject.AddComponent<ContinueClickCatcher>().Owner = this;

            // --- Flecha ---
            _arrow = CreateImage("Arrow", transform);
            _arrow.sprite = _settings.ArrowSprite;
            _arrow.preserveAspect = true;
            var arrowRect = _arrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = Vector2.zero;
            arrowRect.sizeDelta = _settings.ArrowSize;

            // --- Popup (bg + texto + footer) ---
            var popupGo = new GameObject("Popup", typeof(RectTransform));
            popupGo.transform.SetParent(transform, false);
            _popupRoot = (RectTransform)popupGo.transform;
            _popupRoot.anchorMin = _popupRoot.anchorMax = Vector2.zero;

            _popupBackground = CreateImage("Background", _popupRoot);
            var bgRect = _popupBackground.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            _popupBackground.color = _settings.PopupBackgroundColor;
            if (_settings.PopupBackgroundSprite != null)
            {
                _popupBackground.sprite = _settings.PopupBackgroundSprite;
                _popupBackground.type = Image.Type.Sliced;
                if (_settings.PopupBackgroundPpuMultiplier > 0f)
                    _popupBackground.pixelsPerUnitMultiplier = _settings.PopupBackgroundPpuMultiplier;
            }

            _popupText = CreateText("Text", _popupRoot);
            _popupFooter = CreateText("Footer", _popupRoot);
            _popupFooter.fontSize = _settings.PopupFontSize * 0.65f;
            // El guard cubre assets serializados antes de que existiera el campo
            // (un float nuevo deserializa 0 = footer invisible).
            _popupFooter.alpha = _settings.PopupFooterAlpha > 0f ? _settings.PopupFooterAlpha : 0.9f;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            if (_settings.PopupFont != null) text.font = _settings.PopupFont;
            text.fontSize = _settings.PopupFontSize;
            text.color = _settings.PopupTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        // ==================================================================
        // ITutorialOverlayService
        // ==================================================================

        public void Show(TutorialStepDisplayRequest request, Action onContinue = null)
        {
            if (request == null) return;

            bool retarget = IsVisible;
            _request = request;
            _onContinue = onContinue;
            _anchorQuadrant = -1;
            _popupSide = -1;
            _hadAnchor = false;
            _anchorLostAt = -1f;
            _popupCenterInitialized = false;

            if (request.InputPolicy == TutorialInputPolicy.BlockUntilContinue && onContinue == null)
            {
                Debug.LogWarning("[TutorialOverlay] BlockUntilContinue sin onContinue — el click no va a avanzar nada.");
            }

            ApplyText(request);
            ApplyInputPolicy(request.InputPolicy);

            _dimMaterial.SetColor(DimColorId, _settings.DimColor);
            _dimMaterial.SetFloat(FeatherId, _settings.CutoutFeatherPx);

            gameObject.SetActive(true);
            IsVisible = true;
            UpdateLayout(); // primer frame correcto antes del tween

            if (!retarget)
            {
                _showTween.Stop();
                _dimMaterial.SetFloat(ShowId, 0f);
                _showTween = Tween.MaterialProperty(
                    _dimMaterial, ShowId, 1f, _settings.ShowDuration, Ease.OutQuad);
            }
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            SetHotkeysSuppressed(false);
            _showTween.Stop();
            _showTween = Tween.MaterialProperty(
                    _dimMaterial, ShowId, 0f, _settings.HideDuration, Ease.InQuad)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void HideInstant()
        {
            SetHotkeysSuppressed(false);
            _showTween.Stop();
            IsVisible = false;
            _request = null;
            _onContinue = null;
            if (_dimMaterial != null) _dimMaterial.SetFloat(ShowId, 0f);
            gameObject.SetActive(false);
        }

        // ==================================================================
        // Per-frame layout (anchors móviles: pawns, cámara, resize de ventana)
        // ==================================================================

        private void LateUpdate()
        {
            if (!IsVisible || _request == null) return;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 anchorPos = default;
            bool hasAnchor = _request.AnchorKind != TutorialAnchorKind.None
                             && TutorialAnchorResolver.TryResolve(_request, out anchorPos);

            // Gracia ante pérdida transitoria del anchor (zoom/rotación de cámara):
            // sin esto el popup saltaba al centro y volvía frame a frame.
            if (hasAnchor)
            {
                _hadAnchor = true;
                _lastAnchorPos = anchorPos;
                _anchorLostAt = -1f;
            }
            else if (_hadAnchor && _request.AnchorKind != TutorialAnchorKind.None)
            {
                if (_anchorLostAt < 0f) _anchorLostAt = Time.unscaledTime;
                if (Time.unscaledTime - _anchorLostAt <= AnchorLossGraceSeconds)
                {
                    anchorPos = _lastAnchorPos;
                    hasAnchor = true;
                }
            }

            if (!hasAnchor)
            {
                // Centrado sin recorte (paso de texto puro o anchor irresoluble).
                _dimMaterial.SetFloat(CutoutRadiusId, 0f);
                _arrow.enabled = false;
                MeasurePopup(screenSize);
                PlacePopup(SmoothPopupCenter(screenSize * 0.5f));
                return;
            }

            // El recorte que dibuja el shader es circular, pero la flecha se ubica contra
            // la caja cuando el anchor es de UI — ver ResolveArrowPositionForBox.
            float radius;
            var halfSize = Vector2.zero;
            bool hasBox = false;

            if (_request.CutoutRadiusPx > 0f)
            {
                radius = _request.CutoutRadiusPx;
            }
            else if (_request.AnchorKind == TutorialAnchorKind.RectTransform)
            {
                hasBox = TutorialAnchorResolver.TryResolveUiCutout(
                    _request.UiTarget, _request.UiTargetsExtra, _settings.UiCutoutPaddingPx,
                    out _, out halfSize, out radius);
            }
            else
            {
                radius = _settings.DefaultCutoutRadiusPx;
            }

            // Clamp del centro a pantalla (anchor fuera de cuadro sigue señalizable).
            anchorPos.x = Mathf.Clamp(anchorPos.x, 0f, screenSize.x);
            anchorPos.y = Mathf.Clamp(anchorPos.y, 0f, screenSize.y);

            _dimMaterial.SetVector(CutoutCenterId, new Vector4(anchorPos.x, anchorPos.y, 0f, 0f));
            _dimMaterial.SetFloat(CutoutRadiusId, radius);

            // El tamaño se necesita ANTES de elegir el lado. sizeDelta está en
            // unidades de canvas y los anchors en píxeles de pantalla — convertir
            // por el scale factor del canvas.
            float canvasScale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var popupSizePx = MeasurePopup(screenSize) * canvasScale;

            Vector2 popupCenter;
            if (_settings.LegacyQuadrantPlacement)
            {
                _anchorQuadrant = TutorialOverlayLayout.ResolveQuadrantWithHysteresis(
                    anchorPos, screenSize, _anchorQuadrant, _settings.QuadrantHysteresis);
                popupCenter = TutorialOverlayLayout.PopupCenterForQuadrant(
                    _anchorQuadrant, screenSize, _settings.PopupScreenMargin);
            }
            else
            {
                // Popup adyacente al anchor (feedback playtest: el cuadrante opuesto
                // tapaba HUD y tiles). Sin lado válido cae al centro del cuadrante.
                // Guard del gap: 0 = asset serializado antes de que existiera el campo.
                float gap = _settings.PopupAnchorGapPx > 0f ? _settings.PopupAnchorGapPx : 90f;
                var anchorHalf = hasBox ? halfSize : new Vector2(radius, radius);
                _popupSide = TutorialOverlayLayout.ResolveSideWithHysteresis(
                    anchorPos, anchorHalf, popupSizePx, screenSize,
                    gap, _settings.PopupScreenMargin, _popupSide);
                popupCenter = _popupSide >= 0
                    ? TutorialOverlayLayout.PopupCenterForSide(
                        _popupSide, anchorPos, anchorHalf, popupSizePx, screenSize,
                        gap, _settings.PopupScreenMargin)
                    : TutorialOverlayLayout.ResolvePopupCenter(
                        anchorPos, screenSize, _settings.PopupScreenMargin);
            }
            PlacePopup(SmoothPopupCenter(popupCenter));

            if (_request.ShowArrow && _arrow.sprite != null)
            {
                _arrow.enabled = true;
                float bob = 0f;
                if (_settings.ArrowBobAmplitudePx > 0f && _settings.ArrowBobDuration > 0f)
                {
                    _arrowBobPhase += Time.unscaledDeltaTime / _settings.ArrowBobDuration;
                    bob = Mathf.PingPong(_arrowBobPhase, 1f) * _settings.ArrowBobAmplitudePx;
                }

                // El bob va sumado al gap (no al radio) para que en la caja empuje
                // igual sobre el lado que cruza el rayo, sea el corto o el largo.
                var arrowPos = hasBox
                    ? TutorialOverlayLayout.ResolveArrowPositionForBox(
                        anchorPos, halfSize, popupCenter, _settings.ArrowGapPx + bob)
                    : TutorialOverlayLayout.ResolveArrowPosition(
                        anchorPos, radius + bob, popupCenter, _settings.ArrowGapPx);
                _arrow.rectTransform.position = arrowPos;
                _arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f,
                    TutorialOverlayLayout.ResolveArrowRotationZ(arrowPos, anchorPos));
            }
            else
            {
                _arrow.enabled = false;
            }
        }

        /// <summary>
        /// Mide y acomoda los hijos del popup; devuelve el tamaño resultante en
        /// unidades de canvas (multiplicar por el scale factor para píxeles).
        /// </summary>
        private Vector2 MeasurePopup(Vector2 screenSize)
        {
            // El tamaño se deriva del preferred size del TMP contra el ancho máximo.
            float maxTextWidth = Mathf.Min(_settings.PopupMaxWidth, screenSize.x * 0.4f);
            var padding = _settings.PopupPadding;

            _popupText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _popupText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _popupText.rectTransform.pivot = new Vector2(0.5f, 1f);

            var preferred = _popupText.GetPreferredValues(_popupText.text, maxTextWidth, 0f);
            float textWidth = Mathf.Min(preferred.x, maxTextWidth);
            float textHeight = preferred.y;
            _popupText.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);
            _popupText.rectTransform.anchoredPosition = new Vector2(0f, -padding.y);

            float footerHeight = 0f;
            if (_popupFooter.enabled && !string.IsNullOrEmpty(_popupFooter.text))
            {
                var footPreferred = _popupFooter.GetPreferredValues(_popupFooter.text, textWidth, 0f);
                footerHeight = footPreferred.y + 8f;
                _popupFooter.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                _popupFooter.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                _popupFooter.rectTransform.pivot = new Vector2(0.5f, 1f);
                _popupFooter.rectTransform.sizeDelta = new Vector2(textWidth, footPreferred.y);
                _popupFooter.rectTransform.anchoredPosition = new Vector2(0f, -padding.y - textHeight - 8f);
            }

            _popupRoot.sizeDelta = new Vector2(
                textWidth + padding.x * 2f,
                textHeight + footerHeight + padding.y * 2f);
            return _popupRoot.sizeDelta;
        }

        /// <summary>Suaviza el centro del popup con damping exponencial: sigue al
        /// target pero sin teletransportarse cuando la cámara mueve el anchor.</summary>
        private Vector2 SmoothPopupCenter(Vector2 target)
        {
            if (!_popupCenterInitialized || !Application.isPlaying)
            {
                _popupCenterInitialized = true;
                _smoothedPopupCenter = target;
                return target;
            }
            float t = 1f - Mathf.Exp(-PopupSmoothing * Time.unscaledDeltaTime);
            _smoothedPopupCenter = Vector2.Lerp(_smoothedPopupCenter, target, t);
            return _smoothedPopupCenter;
        }

        private void PlacePopup(Vector2 screenCenter)
        {
            // Posicionado por rect.position (píxeles de pantalla — válido en
            // ScreenSpaceOverlay, ver TooltipController.PositionAt).
            _popupRoot.pivot = new Vector2(0.5f, 0.5f);
            _popupRoot.position = new Vector3(screenCenter.x, screenCenter.y, 0f);
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private void ApplyText(TutorialStepDisplayRequest request)
        {
            string text = request.Text ?? string.Empty;
            if (request.HotkeyHint.HasValue)
            {
                string hint = ServiceLocator.TryGetService<IGameplayHotkeyService>(out var hotkeys)
                              && hotkeys != null
                    ? hotkeys.GetKeyHint(request.HotkeyHint.Value)
                    : string.Empty;
                if (string.IsNullOrEmpty(hint)) hint = request.HotkeyHint.Value.ToString();
                text = string.Format(text, hint);
            }
            _popupText.text = text;

            bool blocking = request.InputPolicy == TutorialInputPolicy.BlockUntilContinue;
            _popupFooter.enabled = blocking;
            _popupFooter.text = blocking
                ? LocalizedContent.Ui(TutorialTextKeys.ContinueFooter, _settings.ContinueFooterText)
                : string.Empty;
        }

        private void ApplyInputPolicy(TutorialInputPolicy policy)
        {
            _dim.raycastTarget = policy == TutorialInputPolicy.BlockUntilContinue;

            // BUG-019: el dim solo intercepta pointer — los hotkeys entraban igual por
            // InputSystem durante los pasos "click para continuar". Suprimimos el map
            // Gameplay completo mientras el paso bloquea (la pausa no usa ese map).
            SetHotkeysSuppressed(policy == TutorialInputPolicy.BlockUntilContinue);
        }

        private static void SetHotkeysSuppressed(bool suppressed)
        {
            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out var hotkeys) && hotkeys != null)
                hotkeys.SetSuppressed(suppressed);
        }

        private void HandleContinueClick()
        {
            if (_request == null || _request.InputPolicy != TutorialInputPolicy.BlockUntilContinue) return;
            var callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }

        /// <summary>Click-catcher del dim para pasos BlockUntilContinue. Solo botón
        /// izquierdo: el paso de cámara invita a rotar con el derecho y panear con
        /// el central — soltar esos botones sobre el dim no debe cerrarlo.</summary>
        private sealed class ContinueClickCatcher : MonoBehaviour, IPointerClickHandler
        {
            public TutorialOverlay Owner;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left) return;
                Owner?.HandleContinueClick();
            }
        }
    }
}
