using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Contenido inmutable de un prompt de interacción HUD 2D (bottom-center).
    /// </summary>
    public readonly struct InteractionPromptContent
    {
        public readonly string KeyHint;
        public readonly string Verb;
        public readonly string Title;
        public readonly string Description;
        public readonly int Price;
        public readonly bool CanAfford;

        /// <param name="price">-1 = sin precio (ej. un reward gratis: "[F] Tomar").</param>
        /// <param name="canAfford">Ignorado si <paramref name="price"/> es -1.</param>
        public InteractionPromptContent(string keyHint, string verb, string title, string description,
            int price = -1, bool canAfford = true)
        {
            KeyHint = keyHint;
            Verb = verb;
            Title = title;
            Description = description;
            Price = price;
            CanAfford = canAfford;
        }
    }

    /// <summary>
    /// Panel HUD 2D screen-space (bottom-center) que reemplaza los prompts
    /// world-space por-pedestal — cada <c>ShopItemPedestalInteractable</c> /
    /// <c>CharacterRewardPedestalInteractable</c> armaba su propio Canvas
    /// WorldSpace + TMP diminuto sobre el pedestal, ilegible en la mayoría de
    /// ángulos de cámara. Este panel único vive en screen-space y los
    /// pedestales sólo le pasan contenido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Self-built on-demand + <c>DontDestroyOnLoad</c>, mismo patrón que
    /// <see cref="PersistentUiOverlay"/>.
    /// </para>
    /// <para>
    /// Owner-guard igual a <see cref="Rollgeon.UI.Tooltips.TooltipController"/>:
    /// <see cref="Hide"/> sólo cierra el panel si el <c>ownerId</c> (típicamente
    /// <c>GetInstanceID()</c> del pedestal) coincide con el que lo abrió — así
    /// el pedestal que el jugador está dejando no le cierra el prompt al
    /// pedestal en el que acaba de entrar el mismo frame.
    /// </para>
    /// </remarks>
    public static class InteractionPromptView
    {
        private const string OverlayName = "[InteractionPromptOverlay]";
        private const int SortingOrder = 25000;
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Color PanelColor = new Color(0.078f, 0.078f, 0.118f, 0.88f); // #14141E @ .88
        private static readonly Color TitleColor = new Color(0.949f, 0.949f, 0.961f);        // #F2F2F5
        private static readonly Color DescColor = new Color(0.761f, 0.776f, 0.816f);         // #C2C6D0

        private static Runtime _instance;

        /// <summary>
        /// Muestra (o refresca) el panel con <paramref name="content"/>. El último
        /// llamante siempre "gana" — no hay cola de owners, sólo guard en <see cref="Hide"/>.
        /// </summary>
        public static void Show(int ownerId, in InteractionPromptContent content)
        {
            Show(ownerId, in content, null);
        }

        /// <summary>
        /// Variante con acción de confirmación: el footer se renderiza como un botón
        /// clickeable ("[KEY] Verbo") con el precio al lado, y clickearlo invoca
        /// <paramref name="onConfirm"/> — equivalente a presionar la tecla física
        /// (el caller conecta ambos paths al mismo handler). Con
        /// <c>onConfirm == null</c> el panel se comporta como siempre: footer de
        /// texto, cero raycasts.
        /// </summary>
        public static void Show(int ownerId, in InteractionPromptContent content, Action onConfirm)
        {
            EnsureInstance();
            _instance.ShowContent(ownerId, in content, onConfirm);
        }

        /// <summary>Oculta el panel SOLO si <paramref name="ownerId"/> es el dueño actual.</summary>
        /// <summary>
        /// Prompt de DECISIÓN: dos botones (confirmar / secundario) y polling de teclado
        /// propio, para callers sin MonoBehaviour (servicios). Los callers clásicos de
        /// <see cref="Show(int, in InteractionPromptContent, Action)"/> siguen leyendo la
        /// tecla ellos mismos — acá el prompt es el único que la lee, así no se duplica.
        /// El botón de confirmar respeta <c>CanAfford</c>; el secundario siempre está activo.
        /// </summary>
        public static void ShowChoice(int ownerId, in InteractionPromptContent content, Action onConfirm,
            Key confirmKey, string secondaryKeyHint, string secondaryVerb, Action onSecondary, Key secondaryKey)
        {
            EnsureInstance();
            _instance.ShowContent(ownerId, in content, onConfirm);
            _instance.EnableChoice(confirmKey, secondaryKeyHint, secondaryVerb, onSecondary, secondaryKey);
        }

        /// <summary><c>true</c> mientras un prompt de <see cref="ShowChoice"/> está visible.</summary>
        public static bool IsChoiceActive => _instance != null && _instance.ChoiceActive;

        /// <summary>Simula el click del botón secundario (tests / dev console).</summary>
        public static void ConfirmForTests() => _instance?.HandleConfirmClicked();
        public static void SecondaryForTests() => _instance?.HandleSecondaryClicked();

        public static void Hide(int ownerId)
        {
            if (_instance == null) return;
            _instance.HideIfOwner(ownerId);
        }

        /// <summary>Oculta el panel sin importar el owner (cleanup global).</summary>
        public static void HideForce()
        {
            if (_instance == null) return;
            _instance.HideImmediateOrAnimated();
        }

        /// <summary>
        /// Destruye el overlay y limpia el estado estático. Sólo para teardown de
        /// tests EditMode — sin esto, el overlay creado por un test sobrevive
        /// entre tests (los statics persisten dentro de la sesión del Editor) y
        /// contamina el siguiente test con contenido/owner viejo.
        /// </summary>
        /// <remarks>
        /// Nombrado <c>ResetForTests</c> por convención (no es parte de la API que
        /// consumen los pedestales), pero es <c>public</c> — no <c>internal</c> —
        /// porque este assembly no tiene <c>InternalsVisibleTo("Rollgeon.UI.Tests")</c>
        /// y agregarlo está fuera del alcance de este cambio.
        /// </remarks>
        public static void ResetForTests()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }

        /// <summary>
        /// Formatea el footer "[KEY] Verbo   Precio G" con rich text de color.
        /// <paramref name="price"/> &lt; 0 → sin precio, sólo "[KEY] Verbo". Pura —
        /// testeable sin instancia ni play mode.
        /// </summary>
        public static string BuildFooter(string keyHint, string verb, int price, bool canAfford)
        {
            string keyPart = BuildConfirmLabel(keyHint, verb);
            if (price < 0) return keyPart;
            return $"{keyPart}   {BuildPriceLabel(price, canAfford)}";
        }

        /// <summary>Label del botón de confirmación: "[KEY] Verbo" con la key en amarillo. Pura.</summary>
        public static string BuildConfirmLabel(string keyHint, string verb)
        {
            return $"<color=#FFD75A>[{keyHint}]</color> {verb}";
        }

        /// <summary>
        /// Segmento de precio "N G" coloreado por affordability. <paramref name="price"/>
        /// &lt; 0 → string vacío. Pura.
        /// </summary>
        public static string BuildPriceLabel(int price, bool canAfford)
        {
            if (price < 0) return string.Empty;
            string priceColor = canAfford ? "#FFC533" : "#FF6B6B";
            return $"<color={priceColor}>{price} G</color>";
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject(OverlayName);
            // En EditMode/tests (no isPlaying) DontDestroyOnLoad no aplica — el GO
            // vive y muere con la escena de test, igual que FloatingDamageSpawner
            // resuelve su parent sólo con PersistentUiOverlay cuando isPlaying.
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<Runtime>();
            _instance.Build();
        }

        /// <summary>
        /// MonoBehaviour host de la corrutina de animación — la API estática de
        /// arriba delega acá porque un <c>static class</c> no puede <c>StartCoroutine</c>.
        /// </summary>
        private sealed class Runtime : MonoBehaviour
        {
            private const float ShowDurationSeconds = 0.15f;
            private const float HideDurationSeconds = 0.10f;
            private const float SlideOffsetPixels = 12f;

            private static readonly Color ButtonColor = new Color(0.165f, 0.165f, 0.235f);       // #2A2A3C
            private static readonly Color ButtonHighlight = new Color(0.235f, 0.235f, 0.33f);    // #3C3C54

            private GameObject _panelGO;
            private RectTransform _panelRect;
            private CanvasGroup _canvasGroup;
            private TextMeshProUGUI _titleText;
            private GameObject _descGO;
            private TextMeshProUGUI _descText;
            private TextMeshProUGUI _footerText;
            private GameObject _confirmGO;
            private Button _confirmButton;
            private TextMeshProUGUI _confirmText;
            private Action _confirmCallback;

            // Modo decisión (ShowChoice): segundo botón + polling de teclado propio.
            private GameObject _secondaryGO;
            private Button _secondaryButton;
            private TextMeshProUGUI _secondaryText;
            private Action _secondaryCallback;
            private Key _confirmKey = Key.None;
            private Key _secondaryKey = Key.None;
            private bool _choiceActive;

            public bool ChoiceActive => _choiceActive && _hasOwner;

            private Vector2 _shownAnchoredPos;
            private Vector2 _hiddenAnchoredPos;

            private int _currentOwnerId;
            private bool _hasOwner;
            private Coroutine _activeRoutine;

            public void Build()
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // Sin GraphicRaycaster el canvas no recibe clicks — necesario para el
                // botón de confirmación. Los prompts sin botón siguen sin interceptar
                // nada: todos sus graphics tienen raycastTarget=false y el CanvasGroup
                // apaga blocksRaycasts salvo cuando hay confirm activo.
                gameObject.AddComponent<GraphicRaycaster>();

                BuildPanel();
            }

            private void BuildPanel()
            {
                _panelGO = new GameObject("Panel", typeof(RectTransform));
                _panelRect = (RectTransform)_panelGO.transform;
                _panelRect.SetParent(transform, worldPositionStays: false);
                _panelRect.anchorMin = new Vector2(0.5f, 0f);
                _panelRect.anchorMax = new Vector2(0.5f, 0f);
                _panelRect.pivot = new Vector2(0.5f, 0f);

                _shownAnchoredPos = new Vector2(0f, 96f);
                _hiddenAnchoredPos = _shownAnchoredPos + new Vector2(0f, -SlideOffsetPixels);
                _panelRect.anchoredPosition = _shownAnchoredPos;

                var bg = _panelGO.AddComponent<Image>();
                bg.raycastTarget = false;

                // Fondo con sprite 9-slice si el asset de settings existe (lo crea el
                // installer "Rollgeon → Interaction Prompt → Create Settings"); sin
                // asset —tests EditMode, builds viejas— se conserva el quad de color.
                var settings = Resources.Load<InteractionPromptSettingsSO>(InteractionPromptSettingsSO.ResourcePath);
                if (settings != null && settings.PanelSprite != null)
                {
                    bg.sprite = settings.PanelSprite;
                    bg.type = Image.Type.Sliced;
                    bg.pixelsPerUnitMultiplier = settings.PanelPixelsPerUnitMultiplier;
                    bg.color = Color.white;
                }
                else
                {
                    bg.color = PanelColor;
                }

                var layout = _panelGO.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(18, 18, 14, 14);
                layout.spacing = 4f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = _panelGO.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // El LayoutElement fija el ancho (560) con prioridad sobre el ancho
                // calculado por el VerticalLayoutGroup; el alto queda libre (-1) para
                // que el ContentSizeFitter lo derive del contenido real de los hijos.
                var layoutElement = _panelGO.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 560f;

                _canvasGroup = _panelGO.AddComponent<CanvasGroup>();
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;

                _titleText = BuildTmpChild(_panelRect, "Title", 30f, FontStyles.Bold, TitleColor, wrap: false);
                _titleText.alignment = TextAlignmentOptions.Center;

                _descText = BuildTmpChild(_panelRect, "Description", 22f, FontStyles.Normal, DescColor, wrap: true);
                _descText.alignment = TextAlignmentOptions.Center;
                _descGO = _descText.gameObject;

                _footerText = BuildTmpChild(_panelRect, "Footer", 24f, FontStyles.Normal, Color.white, wrap: false);
                _footerText.alignment = TextAlignmentOptions.Center;
                _footerText.richText = true;

                BuildConfirmButton();
                BuildSecondaryButton();

                _panelGO.SetActive(false);
            }

            private void BuildConfirmButton()
            {
                _confirmGO = new GameObject("ConfirmButton", typeof(RectTransform));
                _confirmGO.transform.SetParent(_panelRect, worldPositionStays: false);

                var image = _confirmGO.AddComponent<Image>();
                image.color = ButtonColor;
                image.raycastTarget = true; // único graphic clickeable de todo el prompt.

                _confirmButton = _confirmGO.AddComponent<Button>();
                _confirmButton.targetGraphic = image;
                var colors = _confirmButton.colors;
                colors.highlightedColor = ButtonHighlight;
                colors.pressedColor = ButtonHighlight;
                _confirmButton.colors = colors;
                _confirmButton.onClick.AddListener(HandleConfirmClicked);

                var layoutElement = _confirmGO.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 44f;

                _confirmText = BuildTmpChild(_confirmGO.transform, "Label", 24f, FontStyles.Bold, Color.white, wrap: false);
                _confirmText.alignment = TextAlignmentOptions.Center;
                _confirmText.richText = true;

                // El label llena el rect del botón (el botón no tiene layout group propio).
                var labelRect = (RectTransform)_confirmText.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                _confirmGO.SetActive(false);
            }

            public void HandleConfirmClicked()
            {
                if (!_hasOwner) return;
                _confirmCallback?.Invoke();
            }

            public void HandleSecondaryClicked()
            {
                if (!_hasOwner || !_choiceActive) return;
                _secondaryCallback?.Invoke();
            }

            private void BuildSecondaryButton()
            {
                _secondaryGO = new GameObject("SecondaryButton", typeof(RectTransform));
                _secondaryGO.transform.SetParent(_panelRect, worldPositionStays: false);

                var image = _secondaryGO.AddComponent<Image>();
                image.color = ButtonColor;
                image.raycastTarget = true;

                _secondaryButton = _secondaryGO.AddComponent<Button>();
                _secondaryButton.targetGraphic = image;
                var colors = _secondaryButton.colors;
                colors.highlightedColor = ButtonHighlight;
                colors.pressedColor = ButtonHighlight;
                _secondaryButton.colors = colors;
                _secondaryButton.onClick.AddListener(HandleSecondaryClicked);

                var layoutElement = _secondaryGO.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 44f;

                _secondaryText = BuildTmpChild(_secondaryGO.transform, "Label", 24f, FontStyles.Bold, Color.white, wrap: false);
                _secondaryText.alignment = TextAlignmentOptions.Center;
                _secondaryText.richText = true;

                var labelRect = (RectTransform)_secondaryText.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                _secondaryGO.SetActive(false);
            }

            public void EnableChoice(Key confirmKey, string secondaryKeyHint, string secondaryVerb,
                Action onSecondary, Key secondaryKey)
            {
                _choiceActive = true;
                _confirmKey = confirmKey;
                _secondaryKey = secondaryKey;
                _secondaryCallback = onSecondary;
                _secondaryText.text = BuildConfirmLabel(secondaryKeyHint, secondaryVerb);
                _secondaryGO.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            }

            private void ClearChoice()
            {
                _choiceActive = false;
                _confirmKey = Key.None;
                _secondaryKey = Key.None;
                _secondaryCallback = null;
                if (_secondaryGO != null) _secondaryGO.SetActive(false);
            }

            // Solo en modo decisión: los callers clásicos leen su tecla ellos mismos.
            private void Update()
            {
                if (!_choiceActive || !_hasOwner) return;
                var keyboard = Keyboard.current;
                if (keyboard == null) return;

                if (_confirmKey != Key.None && keyboard[_confirmKey].wasPressedThisFrame
                    && _confirmButton.interactable)
                {
                    HandleConfirmClicked();
                    return;
                }
                if (_secondaryKey != Key.None && keyboard[_secondaryKey].wasPressedThisFrame)
                    HandleSecondaryClicked();
            }

            private static TextMeshProUGUI BuildTmpChild(Transform parent, string name, float fontSize,
                FontStyles style, Color color, bool wrap)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, worldPositionStays: false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = fontSize;
                tmp.fontStyle = style;
                tmp.color = color;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                return tmp;
            }

            public void ShowContent(int ownerId, in InteractionPromptContent content, Action onConfirm)
            {
                _currentOwnerId = ownerId;
                _hasOwner = true;
                _confirmCallback = onConfirm;
                ClearChoice(); // un Show clásico sobre un prompt de decisión lo degrada a un botón.

                _titleText.text = content.Title ?? string.Empty;

                bool hasDesc = !string.IsNullOrEmpty(content.Description);
                _descGO.SetActive(hasDesc);
                if (hasDesc) _descText.text = content.Description;

                bool hasConfirm = onConfirm != null;
                _confirmGO.SetActive(hasConfirm);
                // El CanvasGroup sólo bloquea raycasts cuando hay botón — un prompt
                // informativo no debe robarle clicks al mundo (contrato original).
                _canvasGroup.blocksRaycasts = hasConfirm;
                _canvasGroup.interactable = hasConfirm;

                if (hasConfirm)
                {
                    _confirmText.text = BuildConfirmLabel(content.KeyHint, content.Verb);
                    _confirmButton.interactable = content.Price < 0 || content.CanAfford;

                    // Con botón, el footer de texto queda sólo para el precio.
                    bool hasPrice = content.Price >= 0;
                    _footerText.gameObject.SetActive(hasPrice);
                    if (hasPrice) _footerText.text = BuildPriceLabel(content.Price, content.CanAfford);
                }
                else
                {
                    _footerText.gameObject.SetActive(true);
                    _footerText.text = BuildFooter(content.KeyHint, content.Verb, content.Price, content.CanAfford);
                }

                // El toggle de _descGO y el texto nuevo cambian la altura preferida —
                // forzar el rebuild ahora para que el layout/tamaño final ya esté
                // resuelto antes de animar o (en EditMode) de setear el estado final.
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

                CancelActiveRoutine();
                _panelGO.SetActive(true);

                if (!Application.isPlaying)
                {
                    _panelRect.anchoredPosition = _shownAnchoredPos;
                    _canvasGroup.alpha = 1f;
                    return;
                }

                _panelRect.anchoredPosition = _hiddenAnchoredPos;
                _canvasGroup.alpha = 0f;
                _activeRoutine = StartCoroutine(AnimateShow());
            }

            public void HideIfOwner(int ownerId)
            {
                if (!_hasOwner || _currentOwnerId != ownerId) return;
                HideImmediateOrAnimated();
            }

            public void HideImmediateOrAnimated()
            {
                _hasOwner = false;
                _confirmCallback = null;
                ClearChoice();
                // Apagar raycasts ya mismo — el fade-out no debe seguir comiendo clicks.
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                CancelActiveRoutine();

                if (!Application.isPlaying)
                {
                    _canvasGroup.alpha = 0f;
                    _panelGO.SetActive(false);
                    return;
                }

                if (!_panelGO.activeSelf) return; // ya oculto — no relanzar la corrutina.
                _activeRoutine = StartCoroutine(AnimateHide());
            }

            private void CancelActiveRoutine()
            {
                if (_activeRoutine == null) return;
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            private IEnumerator AnimateShow()
            {
                float elapsed = 0f;
                while (elapsed < ShowDurationSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = EaseOutCubic(Mathf.Clamp01(elapsed / ShowDurationSeconds));
                    _panelRect.anchoredPosition = Vector2.Lerp(_hiddenAnchoredPos, _shownAnchoredPos, t);
                    _canvasGroup.alpha = t;
                    yield return null;
                }
                _panelRect.anchoredPosition = _shownAnchoredPos;
                _canvasGroup.alpha = 1f;
                _activeRoutine = null;
            }

            private IEnumerator AnimateHide()
            {
                float startAlpha = _canvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < HideDurationSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / HideDurationSeconds);
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                    yield return null;
                }
                _canvasGroup.alpha = 0f;
                _panelGO.SetActive(false);
                _activeRoutine = null;
            }

            private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

            private void OnDestroy()
            {
                if (_instance == this) _instance = null;
            }
        }
    }
}
