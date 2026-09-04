using System.Collections.Generic;
using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Panel singleton del tooltip; los triggers llaman <see cref="Show"/>/<see cref="Hide"/>.
    /// <see cref="_root"/> necesita pivot inferior-centro (0.5, 0): crece hacia arriba.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Controller")]
    public sealed class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        // Por encima de InteractionPromptView (25000): nada debería tapar al tooltip.
        // Internal: StatusHoverBubble se apila un paso por encima del panel.
        internal const int OverlaySortingOrder = 30000;

        [Required("Arrastrar el RectTransform del panel visual (Image + TMP).")]
        [SerializeField] private RectTransform _root;

        [Required("Arrastrar el TMP_Text donde se escribe el texto.")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("Margen por encima del elemento (solo AutoFit).")]
        [SerializeField] private Vector2 _anchorOffset = new Vector2(0f, 12f);

        [Tooltip("Margen mínimo contra el borde de pantalla al clampear.")]
        [SerializeField] private float _screenPadding = 8f;

        [Tooltip("Solo Beside: corrimiento lateral y caída, en píxeles de referencia.")]
        [SerializeField] private Vector2 _sideOffset = new Vector2(110f, 150f);

        [Tooltip("Solo ScreenTopRight: margen contra la esquina (el vertical esquiva los retratos).")]
        [SerializeField] private Vector2 _screenAnchorPadding = new Vector2(16f, 190f);

        [Tooltip("Canvas host. Si null, busca uno via GetComponentInParent en Awake.")]
        [SerializeField] private Canvas _hostCanvas;

        [Tooltip("Columna de tarjetas. Null = panel de texto de siempre.")]
        [SerializeField] private RectTransform _cardsContainer;

        [Tooltip("Prefab de tarjeta. Null = sin columna.")]
        [SerializeField] private TooltipCardView _cardPrefab;

        [Tooltip("Columna del costado (estados aplicados). Null = caen en la de arriba.")]
        [SerializeField] private RectTransform _sideCardsContainer;

        [Tooltip("Fila de slots debajo de la caja. Null = caen en la columna.")]
        [SerializeField] private RectTransform _bottomCardsContainer;

        [Tooltip("Prefab de slot de la fila de abajo. Null = usa el de tarjeta.")]
        [SerializeField] private TooltipCardView _bottomCardPrefab;

        [Title("Banda de identidad")]
        [Tooltip("Nombre de la unidad. Null = banda apagada.")]
        [SerializeField] private TMP_Text _nameLabel;

        [Tooltip("Familia de la unidad, debajo del nombre.")]
        [SerializeField] private TMP_Text _typeLabel;

        [SerializeField] private GameObject _vitalsRoot;
        [SerializeField] private TMP_Text _hpLabel;

        [Tooltip("Fill vertical de la pila de vida: HP actual / max.")]
        [SerializeField] private Image _hpFill;

        [Tooltip("Escudo. Sin escudo se apaga entero: un 0 se lee como escudo roto.")]
        [SerializeField] private GameObject _shieldRoot;
        [SerializeField] private TMP_Text _shieldLabel;

        [Tooltip("Color de la unidad, al pie: no es información.")]
        [SerializeField] private TMP_Text _footerLabel;

        [Tooltip("Candado de fijado. Null = sin candado.")]
        [SerializeField] private GameObject _pinIndicator;

        // Beside cambia el pivot mientras dura; cada Show lo vuelve a fijar.
        private static readonly Vector2 GrowUpPivot = new Vector2(0.5f, 0f);
        private static readonly Vector2 HangRightPivot = new Vector2(0f, 1f);
        private static readonly Vector2 HangLeftPivot = new Vector2(1f, 1f);

        private readonly List<TooltipCardView> _cardSlots = new List<TooltipCardView>();
        private readonly List<TooltipCardView> _sideCardSlots = new List<TooltipCardView>();
        private readonly List<TooltipCardView> _bottomCardSlots = new List<TooltipCardView>();

        private RectTransform _hostCanvasRect;
        private bool _visible;
        // Hide(ownerId) solo cierra si coincide: un hover-exit no cierra el panel de otro.
        private int _currentOwnerId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TooltipController] Otra instancia ya registrada — " +
                                 "destruyendo este duplicado.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            EnsureRefs();
            SetVisible(false);
        }

        // Separado de Awake: el preview de editor corre sin play mode.
        private void EnsureRefs()
        {
            if (_root == null && transform.childCount > 0)
                _root = transform.GetChild(0) as RectTransform;
            // Con columna, el primer TMP descendiente puede ser el titulo de una tarjeta.
            if (_text == null)
                _text = FindHeaderLabel();

            if (_hostCanvas == null) _hostCanvas = GetComponentInParent<Canvas>();
            _hostCanvasRect = _hostCanvas != null ? _hostCanvas.transform as RectTransform : null;

            EnsureOverlaySorting();
        }

        private TMP_Text FindHeaderLabel()
        {
            foreach (var candidate in GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (_cardsContainer != null && candidate.transform.IsChildOf(_cardsContainer))
                    continue;
                if (_sideCardsContainer != null && candidate.transform.IsChildOf(_sideCardsContainer))
                    continue;
                if (_bottomCardsContainer != null && candidate.transform.IsChildOf(_bottomCardsContainer))
                    continue;
                // La banda y el pie tampoco son el párrafo.
                if (candidate == _nameLabel || candidate == _typeLabel || candidate == _hpLabel
                    || candidate == _shieldLabel || candidate == _footerLabel) continue;
                return candidate;
            }
            return null;
        }

        // overrideSorting saca al tooltip de la pelea de orden del HUD. El GraphicRaycaster
        // existe SOLO para las placas de la fila del pie (TooltipStatusSlotHover): todo el
        // resto del panel mantiene raycastTarget=false, así que el mouse sigue pasando de
        // largo salvo exactamente sobre un slot.
        private void EnsureOverlaySorting()
        {
            if (_root == null) return;
            if (!_root.TryGetComponent<Canvas>(out var rootCanvas))
            {
                // Solo en runtime: el preview de editor no debe dirty-ear la escena.
                if (!Application.isPlaying) return;
                rootCanvas = _root.gameObject.AddComponent<Canvas>();
            }
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = OverlaySortingOrder;

            if (Application.isPlaying && !_root.TryGetComponent<GraphicRaycaster>(out _))
                _root.gameObject.AddComponent<GraphicRaycaster>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Muestra el tooltip. <paramref name="ownerId"/> (GetInstanceID del trigger) gatea
        /// <see cref="Hide(int)"/> y <see cref="Toggle"/>.
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId)
            => Show(text, screenPos, ownerId, TooltipPlacementMode.AutoFit);

        /// <summary>
        /// AutoFit suma el offset global y re-posiciona para entrar en el canvas; Fixed usa
        /// <paramref name="screenPos"/> exacto. Ambos clampean a pantalla.
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId, TooltipPlacementMode placement)
            => Show(text, null, screenPos, ownerId, placement);

        /// <summary>
        /// <see cref="TooltipVerticalSide.Below"/> cuelga el panel debajo del anclaje (solo AutoFit).
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId,
            TooltipPlacementMode placement, TooltipVerticalSide side)
            => Show(TooltipContent.FromText(text, null), screenPos, ownerId, placement, side);

        /// <summary>Encabezado + columna de tarjetas.</summary>
        public void Show(string header, IReadOnlyList<StatusIconState> cards, Vector2 screenPos,
                         int ownerId, TooltipPlacementMode placement)
            => Show(TooltipContent.FromText(header, cards), screenPos, ownerId, placement);

        /// <summary>El camino completo: todas las variantes terminan acá.</summary>
        public void Show(in TooltipContent content, Vector2 screenPos, int ownerId,
                         TooltipPlacementMode placement,
                         TooltipVerticalSide side = TooltipVerticalSide.Above)
        {
            // El panel es compartido: cada Show arranca sin candado y el fijado lo re-afirma.
            SetPinned(false);

            ApplyContent(content);
            _currentOwnerId = ownerId;
            SetVisible(true);

            if (placement == TooltipPlacementMode.Beside) PlaceBeside(screenPos);
            else if (placement == TooltipPlacementMode.ScreenTopRight) PlaceTopRight();
            else PlaceOver(screenPos, placement, side);

            ClampToCanvas();
        }

        /// <summary>
        /// Esquina superior derecha del canvas, menos el padding; ignora el punto del trigger.
        /// </summary>
        private void PlaceTopRight()
        {
            if (_root == null || _hostCanvasRect == null) return;

            // Rect local = píxeles de referencia; TransformPoint lo lleva a resolución real.
            _root.pivot = HangLeftPivot;
            var rect = _hostCanvasRect.rect;
            var local = new Vector2(rect.xMax - _screenAnchorPadding.x,
                                    rect.yMax - _screenAnchorPadding.y);
            _root.position = _hostCanvasRect.TransformPoint(local);
        }

        private void PlaceOver(Vector2 screenPos, TooltipPlacementMode placement,
                               TooltipVerticalSide side)
        {
            if (_root != null) _root.pivot = GrowUpPivot;

            Vector2 target;
            if (placement == TooltipPlacementMode.Fixed)
            {
                target = screenPos;
            }
            else if (side == TooltipVerticalSide.Below && _root != null)
            {
                // Colgar "debajo" necesita la altura real del panel con el texto nuevo.
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
                float panelScreenHeight = _root.rect.height * _root.lossyScale.y;
                target = TooltipVerticalPlacement.ComputeAnchorTarget(
                    screenPos, _anchorOffset, panelScreenHeight, side);
            }
            else
            {
                target = screenPos + _anchorOffset;
            }
            PositionAt(target);
        }

        /// <summary>
        /// Cuelga al costado y hacia abajo: lo que disparó el tooltip queda a la vista.
        /// </summary>
        private void PlaceBeside(Vector2 anchor)
        {
            if (_root == null) return;

            float scale = _hostCanvas != null ? _hostCanvas.scaleFactor : 1f;
            var offset = _sideOffset * scale;

            _root.pivot = HangRightPivot;
            PositionAt(anchor + offset);

            // Si no entra de este lado, cuelga del otro: el clamp lo metería sobre lo que se mira.
            if (MeasureClampShift().x >= 0f) return;

            _root.pivot = HangLeftPivot;
            PositionAt(anchor + new Vector2(-offset.x, offset.y));
        }

        /// <summary>Oculta solo si el owner coincide.</summary>
        public void Hide(int ownerId)
        {
            if (_currentOwnerId != ownerId) return;
            SetVisible(false);
            _currentOwnerId = 0;
        }

        /// <summary>Oculta sin importar el owner (usado por cleanup global).</summary>
        public void HideForce()
        {
            SetVisible(false);
            _currentOwnerId = 0;
        }

        /// <summary>Mismo owner = oculta; otro = muestra con el nuevo. Para click triggers.</summary>
        public void Toggle(string text, Vector2 screenPos, int ownerId,
            TooltipPlacementMode placement = TooltipPlacementMode.AutoFit,
            TooltipVerticalSide side = TooltipVerticalSide.Above)
        {
            if (_visible && _currentOwnerId == ownerId)
            {
                SetVisible(false);
                _currentOwnerId = 0;
            }
            else
            {
                Show(text, screenPos, ownerId, placement, side);
            }
        }

        private void PositionAt(Vector2 target)
        {
            if (_root == null) return;

            if (_hostCanvas == null || _hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _root.position = target;
                return;
            }

            if (_hostCanvasRect == null) return;
            var cam = _hostCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _hostCanvasRect, target, cam, out var local))
            {
                _root.localPosition = local;
            }
        }

        /// <summary>Corre el panel lo mínimo para que entre completo en el canvas.</summary>
        private void ClampToCanvas()
        {
            var shift = MeasureClampShift();
            if (shift.sqrMagnitude > 0.0001f)
                _root.position += _hostCanvasRect.TransformVector(new Vector3(shift.x, shift.y, 0f));
        }

        // Separado del clamp: Beside lo pregunta ANTES de aplicar, para elegir el lado.
        private Vector2 MeasureClampShift()
        {
            if (_root == null || _hostCanvasRect == null) return Vector2.zero;

            // El TMP recién recibió texto nuevo — forzar layout para medir el tamaño real.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            var corners = new Vector3[4];
            _root.GetWorldCorners(corners); // 0 = bottom-left, 2 = top-right
            Vector2 min = _hostCanvasRect.InverseTransformPoint(corners[0]);
            Vector2 max = _hostCanvasRect.InverseTransformPoint(corners[2]);

            return ComputeClampShift(min, max, _hostCanvasRect.rect, _screenPadding);
        }

        /// <summary>
        /// Desplazamiento mínimo para meter el rect en <paramref name="bounds"/>; si no entra,
        /// prioriza el borde izquierdo/inferior. Pura para testearla sin canvas.
        /// </summary>
        public static Vector2 ComputeClampShift(Vector2 min, Vector2 max, Rect bounds, float padding)
        {
            Vector2 shift = Vector2.zero;
            if (max.x > bounds.xMax - padding) shift.x = bounds.xMax - padding - max.x;
            if (min.x + shift.x < bounds.xMin + padding) shift.x = bounds.xMin + padding - min.x;
            if (max.y > bounds.yMax - padding) shift.y = bounds.yMax - padding - max.y;
            if (min.y + shift.y < bounds.yMin + padding) shift.y = bounds.yMin + padding - min.y;
            return shift;
        }

        // Todo menos _text es nullable a proposito: sin cablear, el panel de texto de siempre.
        private void ApplyContent(in TooltipContent content)
        {
            int count = content.CardCount;

            if (_text != null)
            {
                _text.text = content.Text ?? string.Empty;
                // Solo, el parrafo ES el tooltip; acompañado, vacio seria un renglon de aire.
                bool aloneInThePanel = count == 0 && !content.HasVitals
                                       && string.IsNullOrEmpty(content.Name);
                _text.gameObject.SetActive(aloneInThePanel || !string.IsNullOrEmpty(content.Text));
            }

            ApplyIdentity(content);

            if (_footerLabel != null)
            {
                _footerLabel.text = content.Flavor ?? string.Empty;
                _footerLabel.gameObject.SetActive(!string.IsNullOrEmpty(content.Flavor));
            }

            if (_cardsContainer == null || _cardPrefab == null) return;

            // Sin contenedores extra cableados, todo cae en la columna de adentro.
            var side = content.SideCards;
            var bottom = content.BottomCards;

            if (_bottomCardsContainer != null)
                FillColumn(_bottomCardsContainer, _bottomCardSlots, bottom, null, _bottomCardPrefab);

            if (_sideCardsContainer == null)
            {
                FillColumn(_cardsContainer, _cardSlots, content.Cards,
                           Concat(side, _bottomCardsContainer == null ? bottom : null));
                return;
            }

            FillColumn(_cardsContainer, _cardSlots, content.Cards,
                       _bottomCardsContainer == null ? bottom : null);
            FillColumn(_sideCardsContainer, _sideCardSlots, side, null);
        }

        // Solo corre en el panel a medio cablear; con los tres contenedores no aloca.
        private static IReadOnlyList<StatusIconState> Concat(IReadOnlyList<StatusIconState> a,
                                                             IReadOnlyList<StatusIconState> b)
        {
            if (a == null || a.Count == 0) return b;
            if (b == null || b.Count == 0) return a;

            var merged = new List<StatusIconState>(a.Count + b.Count);
            merged.AddRange(a);
            merged.AddRange(b);
            return merged;
        }

        private void ApplyIdentity(in TooltipContent content)
        {
            if (_nameLabel != null)
            {
                _nameLabel.text = content.Name ?? string.Empty;
                _nameLabel.gameObject.SetActive(!string.IsNullOrEmpty(content.Name));
            }

            if (_typeLabel != null)
            {
                _typeLabel.text = content.Type ?? string.Empty;
                _typeLabel.gameObject.SetActive(!string.IsNullOrEmpty(content.Type));
            }

            if (_vitalsRoot != null) _vitalsRoot.SetActive(content.HasVitals);
            if (!content.HasVitals) return;

            // A diferencia de la cabeza, acá el número lleva también el máximo.
            if (_hpLabel != null)
                _hpLabel.text = $"{content.Health.Value}/{content.MaxHealth.Value}";
            if (_hpFill != null)
                _hpFill.fillAmount = content.MaxHealth.Value > 0
                    ? Mathf.Clamp01(content.Health.Value / (float)content.MaxHealth.Value)
                    : 0f;

            // Un escudo en cero no es un escudo roto, es que la unidad no usa escudo.
            int shield = content.Shield ?? 0;
            if (_shieldRoot != null) _shieldRoot.SetActive(shield > 0);
            if (_shieldLabel != null && shield > 0) _shieldLabel.text = shield.ToString();
        }

        // Los slots se reusan y solo se apagan: destruir/instanciar por hover seria churn.
        // Dos listas y no una concatenada, para no alocar en cada hover.
        private void FillColumn(RectTransform container, List<TooltipCardView> slots,
                                IReadOnlyList<StatusIconState> first,
                                IReadOnlyList<StatusIconState> second,
                                TooltipCardView prefab = null)
        {
            // Cada columna puede traer su propia forma de tarjeta; sin una, la de siempre.
            if (prefab == null) prefab = _cardPrefab;

            int firstCount = first?.Count ?? 0;
            int total = firstCount + (second?.Count ?? 0);

            container.gameObject.SetActive(total > 0);
            while (slots.Count < total) slots.Add(Instantiate(prefab, container));

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < total;
                slots[i].gameObject.SetActive(used);
                if (used) slots[i].Show(i < firstCount ? first[i] : second[i - firstCount]);
            }
        }

        /// <summary>Prende/apaga el candado de fijado. Lo maneja el trigger dueño del pin.</summary>
        public void SetPinned(bool pinned)
        {
            if (_pinIndicator == null) return;

            // Con la view animada, el candado se cierra al fijar y se abre antes de
            // irse; sin ella (panel viejo, tests), el SetActive de siempre.
            if (_pinIndicator.TryGetComponent<TooltipPinLockView>(out var view))
                view.SetPinned(pinned);
            else
                _pinIndicator.SetActive(pinned);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.gameObject.SetActive(visible);
            if (!visible) SetPinned(false);
            // overrideSorting no persiste si se seteó con el GO inactivo — re-aplicar
            // con el panel ya activo.
            if (visible) EnsureOverlaySorting();
        }

#if UNITY_EDITOR
        // No colisiona con GetInstanceID() de ningún trigger real.
        internal const int EditorPreviewOwnerId = int.MinValue;

        // Ancestros que el preview activó; se restauran al ocultar.
        private readonly System.Collections.Generic.List<GameObject> _editorPreviewActivated =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Panel real con texto de ejemplo, sin play mode; activa la jerarquía si hace falta.
        /// </summary>
        internal void EditorPreview(string text, Vector2 screenPos, TooltipPlacementMode placement)
        {
            EditorPreviewBegin();
            Show(text, screenPos, EditorPreviewOwnerId, placement);
        }

        /// <summary>
        /// El panel entero sin play mode. Público: lo llama el assembly de editor, que no
        /// ve los internals de este.
        /// </summary>
        public void EditorPreview(in TooltipContent content, Vector2 screenPos,
                                  TooltipPlacementMode placement)
        {
            EditorPreviewBegin();
            Show(content, screenPos, EditorPreviewOwnerId, placement);
        }

        // activeSelf=true con un ancestro apagado sigue invisible; activar ANTES de Show,
        // para que el rebuild del clamp mida el tamaño real.
        private void EditorPreviewBegin()
        {
            EnsureRefs();

            for (var t = transform; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                _editorPreviewActivated.Add(t.gameObject);
            }
        }

        /// <summary>
        /// Tira las tarjetas instanciadas: no son instancias de prefab y no heredan los
        /// cambios posteriores del asset.
        /// </summary>
        public void EditorPreviewResetCards()
        {
            EnsureRefs();
            ResetSlots(_cardsContainer, _cardSlots);
            ResetSlots(_sideCardsContainer, _sideCardSlots);
            ResetSlots(_bottomCardsContainer, _bottomCardSlots);
        }

        // Barre los HIJOS del contenedor y no sólo la lista: el domain reload vacía la lista
        // pero las tarjetas instanciadas quedan huérfanas en la escena.
        private static void ResetSlots(RectTransform container, List<TooltipCardView> slots)
        {
            slots.Clear();
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child.GetComponent<TooltipCardView>() != null)
                    DestroyImmediate(child.gameObject);
            }
        }

        public void EditorPreviewHide()
        {
            EnsureRefs();
            HideForce();

            foreach (var go in _editorPreviewActivated)
                if (go != null) go.SetActive(false);
            _editorPreviewActivated.Clear();
        }
#endif
    }
}
