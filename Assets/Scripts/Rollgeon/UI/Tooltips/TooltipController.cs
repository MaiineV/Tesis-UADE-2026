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
    /// El panel debe tener pivot top-left (o ajustar el cursorOffset) para que el cursor
    /// no quede tapado por el tooltip.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Controller")]
    public sealed class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Required("Arrastrar el RectTransform del panel visual (Image + TMP).")]
        [SerializeField] private RectTransform _root;

        [Required("Arrastrar el TMP_Text donde se escribe el texto.")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("Offset en píxeles desde el punto-pantalla del anchor. Default (16, -16): " +
                 "un poco a la derecha y abajo del objeto.")]
        [SerializeField] private Vector2 _anchorOffset = new Vector2(16f, -16f);

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

            // Auto-resolve si no se cableo en Inspector (convencion: primer RectTransform
            // hijo es _root, primer TMP_Text descendiente es _text). Esto deja el setup
            // "agregar componente y crear sub-objetos Panel/Text" funcionando sin Drag&Drop.
            if (_root == null && transform.childCount > 0)
                _root = transform.GetChild(0) as RectTransform;
            if (_text == null)
                _text = GetComponentInChildren<TMP_Text>(includeInactive: true);

            if (_hostCanvas == null) _hostCanvas = GetComponentInParent<Canvas>();
            _hostCanvasRect = _hostCanvas != null ? _hostCanvas.transform as RectTransform : null;

            SetVisible(false);
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
        {
            if (_text != null) _text.text = text ?? string.Empty;
            _currentOwnerId = ownerId;
            SetVisible(true);
            PositionAt(screenPos);
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
        public void Toggle(string text, Vector2 screenPos, int ownerId)
        {
            if (_visible && _currentOwnerId == ownerId)
            {
                SetVisible(false);
                _currentOwnerId = 0;
            }
            else
            {
                Show(text, screenPos, ownerId);
            }
        }

        private void PositionAt(Vector2 screenPos)
        {
            if (_root == null) return;

            Vector2 target = screenPos + _anchorOffset;

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

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.gameObject.SetActive(visible);
        }
    }
}
