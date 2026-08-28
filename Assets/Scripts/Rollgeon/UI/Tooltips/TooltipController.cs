using System.Collections.Generic;
using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Panel UI singleton que muestra un tooltip flotante siguiendo al cursor.
    /// Los triggers (<see cref="UITooltipTrigger"/>, <see cref="WorldTooltipTrigger"/>)
    /// invocan <see cref="Show"/>/<see cref="Hide"/>. Esperado que viva en el HUD canvas
    /// como un único GameObject, con el <see cref="_root"/> apuntando al panel visual
    /// (background + texto) que se toggle-a por activeSelf.
    /// </summary>
    /// <remarks>
    /// <b>Layout esperado:</b> un sub-GameObject "Panel" con Image de fondo + TMP_Text hijo.
    /// El panel debe tener pivot inferior-centro (0.5, 0): crece hacia arriba, centrado
    /// sobre el punto de anclaje. En AutoFit ese punto ya es el BORDE SUPERIOR del
    /// elemento (ver <see cref="TooltipPlacement.ScreenPosOf"/>, BUG-041) — el offset
    /// default suma un margen chico encima para que no quede pegado.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Controller")]
    public sealed class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        // Por encima de InteractionPromptView (25000) — el tooltip sigue al cursor,
        // nada debería taparlo.
        private const int OverlaySortingOrder = 30000;

        [Required("Arrastrar el RectTransform del panel visual (Image + TMP).")]
        [SerializeField] private RectTransform _root;

        [Required("Arrastrar el TMP_Text donde se escribe el texto.")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("Offset en píxeles desde el punto-pantalla del anchor. Default (0, 12): " +
                 "en AutoFit el anchor ya es el borde SUPERIOR del rect (BUG-041) y el " +
                 "panel tiene pivot inferior-centro, así que este offset es el margen " +
                 "extra por encima del elemento. Solo aplica en modo AutoFit — en Fixed " +
                 "no se suma (el trigger ya resolvió su propia posición), pero igual se " +
                 "clampea a pantalla.")]
        [SerializeField] private Vector2 _anchorOffset = new Vector2(0f, 12f);

        [Tooltip("Margen mínimo en píxeles del canvas entre el panel y el borde de la " +
                 "pantalla cuando AutoFit re-posiciona el tooltip.")]
        [SerializeField] private float _screenPadding = 8f;

        [Tooltip("Solo en modo Beside: cuánto se corre el panel al costado del punto de " +
                 "anclaje y cuánto queda su borde superior por encima de él. En píxeles de " +
                 "referencia del canvas — se escala con el scaleFactor, igual que el offset " +
                 "de los triggers Fixed.")]
        [SerializeField] private Vector2 _sideOffset = new Vector2(110f, 150f);

        [Tooltip("Canvas host. Si null, busca uno via GetComponentInParent en Awake.")]
        [SerializeField] private Canvas _hostCanvas;

        [Tooltip("Contenedor de la columna de tarjetas (\"Cards\"), hermano de _text bajo " +
                 "el panel. Null a propósito: sin cablear, el panel se comporta exactamente " +
                 "como el tooltip de texto de siempre — es lo que mantiene vivos todos los " +
                 "tooltips existentes y TooltipPlacementTests mientras se completa el wiring " +
                 "del prefab (ver Rollgeon/Tooltips/2 - Wire Card Column Into Tooltip Panel).")]
        [SerializeField] private RectTransform _cardsContainer;

        [Tooltip("Prefab de una tarjeta de la columna. Null junto con _cardsContainer = " +
                 "sin columna, mismo comportamiento que antes.")]
        [SerializeField] private TooltipCardView _cardPrefab;

        [Tooltip("Segunda columna, al costado de la de arriba: los estados que le aplicaron. " +
                 "Null = caen en la columna de arriba, y el panel queda como antes.")]
        [SerializeField] private RectTransform _sideCardsContainer;

        [Title("Banda de identidad")]
        [Tooltip("Nombre de la unidad. Null, como toda esta banda: un tooltip que no trae " +
                 "identidad deja el bloque apagado y el panel es el de siempre.")]
        [SerializeField] private TMP_Text _nameLabel;

        [Tooltip("Familia de la unidad, debajo del nombre. Se apaga sola cuando el enemigo no " +
                 "tiene familia autorada.")]
        [SerializeField] private TMP_Text _typeLabel;

        [SerializeField] private GameObject _vitalsRoot;
        [SerializeField] private TMP_Text _hpLabel;

        [Tooltip("El par ícono+número del escudo. Se apaga entero cuando la unidad no tiene: " +
                 "un \"0\" al lado de un escudo se lee como que el escudo existe y está roto.")]
        [SerializeField] private GameObject _shieldRoot;
        [SerializeField] private TMP_Text _shieldLabel;

        [Tooltip("Color de la unidad, al pie. Separado de _text porque no es información y no " +
                 "puede quedarse con el arriba del panel.")]
        [SerializeField] private TMP_Text _footerLabel;

        // El panel crece hacia arriba centrado sobre el punto: es lo que esperan los tooltips
        // de texto. Beside lo cambia mientras dura, así que cada Show lo vuelve a fijar.
        private static readonly Vector2 GrowUpPivot = new Vector2(0.5f, 0f);
        private static readonly Vector2 HangRightPivot = new Vector2(0f, 1f);
        private static readonly Vector2 HangLeftPivot = new Vector2(1f, 1f);

        private readonly List<TooltipCardView> _cardSlots = new List<TooltipCardView>();
        private readonly List<TooltipCardView> _sideCardSlots = new List<TooltipCardView>();

        private RectTransform _hostCanvasRect;
        private bool _visible;
        // Identifica al trigger dueño del tooltip actual. Las llamadas a Hide(ownerId) solo
        // cierran si coinciden — evita que el hover-exit de la poción cierre un tooltip
        // que recién abrió un click en la puerta. Toggle(ownerId) cierra si coincide,
        // sino muestra con nuevo owner.
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

        // Auto-resolve si no se cableo en Inspector (convencion: primer RectTransform
        // hijo es _root, primer TMP_Text descendiente es _text). Esto deja el setup
        // "agregar componente y crear sub-objetos Panel/Text" funcionando sin Drag&Drop.
        // Separado de Awake para que el preview de editor funcione sin play mode.
        private void EnsureRefs()
        {
            if (_root == null && transform.childCount > 0)
                _root = transform.GetChild(0) as RectTransform;
            // El auto-resolve toma el primer TMP descendiente, y una vez que existe la columna
            // ese primero puede ser el titulo de una tarjeta. Sin columna cableada el camino es
            // identico al de siempre.
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
                // Los labels de la banda y del pie tampoco son el párrafo: si el auto-resolve
                // se quedara con el nombre, un tooltip de texto escribiría en el renglón grande.
                if (candidate == _nameLabel || candidate == _typeLabel || candidate == _hpLabel
                    || candidate == _shieldLabel || candidate == _footerLabel) continue;
                return candidate;
            }
            return null;
        }

        // El orden de jerarquía dentro del canvas HUD dejaba el tooltip DEBAJO de
        // pantallas hermanas (ej. el panel del Altar de Encantamiento). Un Canvas
        // anidado con overrideSorting lo saca de esa pelea. Sin GraphicRaycaster
        // a propósito: el tooltip nunca debe interceptar el mouse.
        private void EnsureOverlaySorting()
        {
            if (_root == null) return;
            if (!_root.TryGetComponent<Canvas>(out var rootCanvas))
            {
                // Solo en runtime — el preview de editor no debe dirty-ear la escena
                // agregando componentes.
                if (!Application.isPlaying) return;
                rootCanvas = _root.gameObject.AddComponent<Canvas>();
            }
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = OverlaySortingOrder;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Muestra el tooltip anclado al punto-pantalla provisto. <paramref name="ownerId"/>
        /// identifica al trigger (típicamente <c>GetInstanceID()</c>) — usado por
        /// <see cref="Hide(int)"/> y <see cref="Toggle"/> para evitar que otro trigger
        /// cierre/sobrescriba un tooltip que no le pertenece.
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId)
            => Show(text, screenPos, ownerId, TooltipPlacementMode.AutoFit);

        /// <summary>
        /// Variante con modo de posicionamiento. <see cref="TooltipPlacementMode.AutoFit"/>
        /// aplica el offset global y re-posiciona para que el panel entre completo en el
        /// canvas; <see cref="TooltipPlacementMode.Fixed"/> usa <paramref name="screenPos"/>
        /// exacto sin offset (el trigger ya resolvió anchor + offset configurados). Ambos
        /// modos clampean al canvas — Fixed también puede irse de pantalla en resoluciones
        /// chicas o cerca de un borde, y el clamp es una red de seguridad, no invalida la
        /// posición configurada salvo que efectivamente se salga.
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId, TooltipPlacementMode placement)
            => Show(text, null, screenPos, ownerId, placement);

        /// <summary>
        /// Encabezado + columna de tarjetas, una por cosa en juego. Con <paramref name="cards"/>
        /// nulo o vacío el panel se comporta exactamente igual que antes: sólo el encabezado.
        /// </summary>
        public void Show(string header, IReadOnlyList<StatusIconState> cards, Vector2 screenPos,
                         int ownerId, TooltipPlacementMode placement)
            => Show(TooltipContent.FromText(header, cards), screenPos, ownerId, placement);

        /// <summary>
        /// El camino completo: banda de identidad, columna y pie. Todo lo demás termina acá con
        /// un <see cref="TooltipContent"/> que sólo trae texto.
        /// </summary>
        public void Show(in TooltipContent content, Vector2 screenPos, int ownerId,
                         TooltipPlacementMode placement)
        {
            ApplyContent(content);
            _currentOwnerId = ownerId;
            SetVisible(true);

            if (placement == TooltipPlacementMode.Beside) PlaceBeside(screenPos);
            else PlaceOver(screenPos, placement);

            ClampToCanvas();
        }

        private void PlaceOver(Vector2 screenPos, TooltipPlacementMode placement)
        {
            if (_root != null) _root.pivot = GrowUpPivot;
            PositionAt(placement == TooltipPlacementMode.Fixed
                ? screenPos
                : screenPos + _anchorOffset);
        }

        /// <summary>
        /// Cuelga el panel al costado del punto y hacia abajo: el borde superior arranca apenas
        /// por encima del anclaje, así lo que disparó el tooltip queda a la vista al lado.
        /// </summary>
        private void PlaceBeside(Vector2 anchor)
        {
            if (_root == null) return;

            float scale = _hostCanvas != null ? _hostCanvas.scaleFactor : 1f;
            var offset = _sideOffset * scale;

            _root.pivot = HangRightPivot;
            PositionAt(anchor + offset);

            // Si para entrar en pantalla habría que arrastrarlo a la izquierda, cuelga del otro
            // lado: el clamp lo metería justo encima de lo que este modo evita tapar.
            if (MeasureClampShift().x >= 0f) return;

            _root.pivot = HangLeftPivot;
            PositionAt(anchor + new Vector2(-offset.x, offset.y));
        }

        /// <summary>
        /// Oculta el tooltip SOLO si el owner actual coincide con <paramref name="ownerId"/>.
        /// Permite que un hover-exit no cierre un tooltip que abrió otro trigger.
        /// </summary>
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

        /// <summary>
        /// Toggle: si el owner actual == <paramref name="ownerId"/>, oculta. Si no, muestra
        /// con el nuevo owner. Usado por click triggers (puerta).
        /// </summary>
        public void Toggle(string text, Vector2 screenPos, int ownerId,
            TooltipPlacementMode placement = TooltipPlacementMode.AutoFit)
        {
            if (_visible && _currentOwnerId == ownerId)
            {
                SetVisible(false);
                _currentOwnerId = 0;
            }
            else
            {
                Show(text, screenPos, ownerId, placement);
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

        /// <summary>
        /// Re-posiciona el panel lo mínimo necesario para que quede COMPLETO dentro del
        /// rect del canvas (con <see cref="_screenPadding"/> de margen). El default de
        /// anchor abajo-derecha hacía que tooltips cerca del borde quedaran cortados.
        /// </summary>
        /// <remarks>
        /// <see cref="RectTransform.GetWorldCorners"/> devuelve los 4 vértices reales del
        /// rect ya resueltos (pivot incluido) — el cálculo de bordes es agnóstico al pivot
        /// del panel, así que el pivot inferior-centro (0.5, 0) no necesita casos especiales
        /// acá; el shift se aplica sobre <c>_root.position</c> (el pivot), que desplaza el
        /// rect completo por igual sin importar dónde esté el pivot dentro de él.
        /// </remarks>
        private void ClampToCanvas()
        {
            var shift = MeasureClampShift();
            if (shift.sqrMagnitude > 0.0001f)
                _root.position += _hostCanvasRect.TransformVector(new Vector3(shift.x, shift.y, 0f));
        }

        // Cuánto habría que correr el panel para que entre. Separado del clamp porque el modo
        // Beside lo pregunta ANTES de aplicarlo, para decidir de qué lado colgar.
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
        /// Desplazamiento mínimo para meter el rect [<paramref name="min"/>, <paramref name="max"/>]
        /// dentro de <paramref name="bounds"/> con <paramref name="padding"/> de margen.
        /// Si el rect es más grande que los bounds, prioriza el borde izquierdo/inferior.
        /// Pura para poder testearla sin canvas real.
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

        // Banda, parrafo, columna y pie en un solo lugar. Todos los campos menos _text son
        // nullables a proposito: sin ellos el panel es exactamente el de siempre, que es lo que
        // mantiene andando a todos los tooltips de texto que ya existen.
        private void ApplyContent(in TooltipContent content)
        {
            int count = content.CardCount;

            if (_text != null)
            {
                _text.text = content.Text ?? string.Empty;
                // Sin nada mas el parrafo ES el tooltip y no se apaga nunca: apagarlo dejaria un
                // panel vacio. Con banda o columna, un parrafo vacio solo agregaria un renglon
                // alto de nada en el medio.
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

            // Sin segunda columna cableada, lo del costado se dibuja en la de arriba: el panel
            // queda exactamente como antes en vez de perder tarjetas, que es lo que hace que este
            // slot pueda faltar de verdad.
            if (_sideCardsContainer == null)
            {
                FillColumn(_cardsContainer, _cardSlots, content.Cards, content.SideCards);
                return;
            }

            FillColumn(_cardsContainer, _cardSlots, content.Cards, null);
            FillColumn(_sideCardsContainer, _sideCardSlots, content.SideCards, null);
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

            if (_hpLabel != null)
                _hpLabel.text = $"{content.Health.Value}/{content.MaxHealth.Value}";

            // Un escudo en cero no es un escudo roto, es que la unidad no usa escudo.
            int shield = content.Shield ?? 0;
            if (_shieldRoot != null) _shieldRoot.SetActive(shield > 0);
            if (_shieldLabel != null && shield > 0) _shieldLabel.text = shield.ToString();
        }

        // Los slots se reusan y solo se apagan, igual que PlayerStatusIconsView.EnsureSlots: el
        // panel se reabre en cada hover y destruir/instanciar por hover seria churn por mover el
        // mouse.
        //
        // Dos listas y no una concatenada: concatenar alocaria una lista nueva en cada hover, y el
        // caso de las dos juntas es solo el del panel sin segunda columna.
        private void FillColumn(RectTransform container, List<TooltipCardView> slots,
                                IReadOnlyList<StatusIconState> first,
                                IReadOnlyList<StatusIconState> second)
        {
            int firstCount = first?.Count ?? 0;
            int total = firstCount + (second?.Count ?? 0);

            container.gameObject.SetActive(total > 0);
            while (slots.Count < total) slots.Add(Instantiate(_cardPrefab, container));

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < total;
                slots[i].gameObject.SetActive(used);
                if (used) slots[i].Show(i < firstCount ? first[i] : second[i - firstCount]);
            }
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.gameObject.SetActive(visible);
            // overrideSorting no persiste si se seteó con el GO inactivo — re-aplicar
            // con el panel ya activo.
            if (visible) EnsureOverlaySorting();
        }

#if UNITY_EDITOR
        // Owner reservado para el preview de editor — no colisiona con GetInstanceID()
        // de ningún trigger real.
        internal const int EditorPreviewOwnerId = int.MinValue;

        // Ancestros (incluido este GO) que el preview activó porque estaban inactivos
        // en la escena de edición — el TooltipController vive apagado hasta que el
        // sistema de pantallas lo activa en runtime. Se restauran al ocultar el preview.
        private readonly System.Collections.Generic.List<GameObject> _editorPreviewActivated =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Muestra el panel real con texto de ejemplo SIN play mode, para previsualizar
        /// en el Game view qué espacio ocupa el tooltip en la posición configurada.
        /// Invocado por los botones de preview de los triggers. Activa temporalmente la
        /// jerarquía del controller si está inactiva (se restaura en
        /// <see cref="EditorPreviewHide"/>).
        /// </summary>
        internal void EditorPreview(string text, Vector2 screenPos, TooltipPlacementMode placement)
        {
            EnsureRefs();

            // Sin esto el panel se "activa" pero nunca se ve: activeSelf=true con un
            // ancestro apagado sigue siendo invisible. Activar ANTES de Show para que
            // el layout rebuild del clamp mida el tamaño real.
            for (var t = transform; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                _editorPreviewActivated.Add(t.gameObject);
            }

            Show(text, screenPos, EditorPreviewOwnerId, placement);
        }

        internal void EditorPreviewHide()
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
