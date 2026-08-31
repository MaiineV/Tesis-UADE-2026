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

        [Tooltip("Canvas host. Si null, busca uno via GetComponentInParent en Awake.")]
        [SerializeField] private Canvas _hostCanvas;

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
            if (_text == null)
                _text = GetComponentInChildren<TMP_Text>(includeInactive: true);

            if (_hostCanvas == null) _hostCanvas = GetComponentInParent<Canvas>();
            _hostCanvasRect = _hostCanvas != null ? _hostCanvas.transform as RectTransform : null;

            EnsureOverlaySorting();
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
            => Show(text, screenPos, ownerId, placement, TooltipVerticalSide.Above);

        /// <summary>
        /// Variante con lado vertical (BUG-075). <see cref="TooltipVerticalSide.Below"/>
        /// cuelga el panel entero DEBAJO del punto de anclaje — usado por los tooltips de
        /// enemigos (anclados a los pies del pawn) para no tapar el modelo. Solo aplica en
        /// AutoFit; el clamp a pantalla corre igual, así que un enemigo pegado al borde
        /// inferior no deja el tooltip afuera.
        /// </summary>
        public void Show(string text, Vector2 screenPos, int ownerId,
            TooltipPlacementMode placement, TooltipVerticalSide side)
        {
            if (_text != null) _text.text = text ?? string.Empty;
            _currentOwnerId = ownerId;
            SetVisible(true);

            Vector2 target;
            if (placement == TooltipPlacementMode.Fixed)
            {
                target = screenPos;
            }
            else if (side == TooltipVerticalSide.Below && _root != null)
            {
                // Medir la altura real del panel con el texto nuevo antes de colocarlo
                // — colgar "debajo" necesita saber cuánto ocupa.
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
            ClampToCanvas();
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
            if (_root == null || _hostCanvasRect == null) return;

            // El TMP recién recibió texto nuevo — forzar layout para medir el tamaño real.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            var corners = new Vector3[4];
            _root.GetWorldCorners(corners); // 0 = bottom-left, 2 = top-right
            Vector2 min = _hostCanvasRect.InverseTransformPoint(corners[0]);
            Vector2 max = _hostCanvasRect.InverseTransformPoint(corners[2]);

            var shift = ComputeClampShift(min, max, _hostCanvasRect.rect, _screenPadding);
            if (shift.sqrMagnitude > 0.0001f)
                _root.position += _hostCanvasRect.TransformVector(new Vector3(shift.x, shift.y, 0f));
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
