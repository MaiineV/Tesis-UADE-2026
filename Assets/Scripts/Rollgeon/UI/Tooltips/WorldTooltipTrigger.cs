using System;
using System.Collections.Generic;
using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rollgeon.UI.Tooltips
{
    /// <summary>Modo de activación de un <see cref="WorldTooltipTrigger"/>.</summary>
    public enum WorldTooltipMode
    {
        /// <summary>Click toggle: click muestra, click otra vez sobre el mismo objeto oculta.</summary>
        Click,
        /// <summary>Hover: cursor sobre el collider muestra, sale oculta.</summary>
        Hover,
    }

    /// <summary>
    /// Trigger de tooltip para objetos del mundo 3D. Requiere un <see cref="Collider"/>
    /// en el GameObject. El texto se ancla al punto-pantalla del objeto.
    /// </summary>
    /// <remarks>
    /// <b>Por qué Update + Physics.Raycast en lugar de OnMouseDown / OnMouseEnter</b>:
    /// los callbacks legacy del MonoBehaviour son interceptados por Unity cuando hay un
    /// Canvas con GraphicRaycaster cubriendo la pantalla (típico en juegos con HUD que
    /// ocupa toda la pantalla aunque sea con paneles transparentes). El raycast manual
    /// chequea explícitamente <see cref="EventSystem.IsPointerOverGameObject"/> y, si el
    /// cursor NO está sobre UI, hace su propio <see cref="Physics.Raycast"/> y dispara
    /// si pega a este collider. Funciona aunque el HUD ocupe toda la pantalla.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/World Tooltip Trigger")]
    public sealed class WorldTooltipTrigger : MonoBehaviour
    {
        /// <summary>
        /// Provider del texto. Si <c>null</c> al primer uso, el trigger intenta auto-resolver
        /// un <see cref="IHasTooltipInfo"/> en este GameObject o en sus padres/hijos.
        /// </summary>
        public Func<string> TextProvider;

        /// <summary>
        /// Tarjetas del panel, bajo el encabezado que da <see cref="TextProvider"/>. <c>null</c> =
        /// tooltip de texto y nada más.
        /// </summary>
        public Func<IReadOnlyList<StatusIconState>> CardsProvider;

        /// <summary>
        /// Provider de la banda de identidad — nombre, vitales y color al pie. Gana sobre
        /// <see cref="TextProvider"/> cuando está: quien sabe describirse entero no tiene por qué
        /// aplanarse a un párrafo.
        /// </summary>
        public Func<TooltipContent> ContentProvider;

        /// <summary>
        /// Entra (<c>true</c>) y sale (<c>false</c>) el hover. Lo consume quien además del texto
        /// tiene que pintar algo — el tooltip se resuelve solo acá adentro.
        /// </summary>
        public event Action<bool> HoverChanged;

        [SerializeField] private WorldTooltipMode _mode = WorldTooltipMode.Click;

        /// <summary>
        /// Modo de activación. Escribible para los triggers que se agregan por código: el default
        /// serializado es <see cref="WorldTooltipMode.Click"/> (la puerta), y un
        /// <c>AddComponent</c> no puede elegir otro desde el Inspector.
        /// </summary>
        public WorldTooltipMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;

                // El hover activo muere con el cambio de modo: si se pasa a Click quedaría un
                // tooltip abierto que ningún Update va a cerrar.
                SetHover(false, null);
            }
        }

        [SerializeField] private TooltipPlacementSettings _placement = new TooltipPlacementSettings();

        /// <summary>
        /// Dónde se dibuja el panel respecto del objeto. Escribible por lo mismo que
        /// <see cref="Mode"/>: los triggers que se cuelgan por código no pasan por el Inspector.
        /// </summary>
        public TooltipPlacementMode Placement
        {
            get => _placement.Mode;
            set => _placement.Mode = value;
        }

        [Tooltip("Cámara usada para raycast + WorldToScreenPoint. Null = Camera.main en runtime.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Distancia máxima del raycast al cursor en world units. Default 100.")]
        [SerializeField] private float _raycastDistance = 100f;

        private int _ownerId;
        private bool _hoverActive;

        private void Awake()
        {
            _ownerId = GetInstanceID();
        }

        private void Update()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;
            if (!TryGetMouseScreenPos(out var mouseScreen)) return;

            // Si el cursor está sobre UI (botón, panel del HUD), no procesar hits del mundo.
            bool pointerOverUI = EventSystem.current != null
                                 && EventSystem.current.IsPointerOverGameObject();

            bool hitMe = !pointerOverUI && RaycastHitsMe(cam, mouseScreen);

            if (_mode == WorldTooltipMode.Hover)
            {
                SetHover(hitMe, cam);
                return;
            }

            if (hitMe && MouseLeftPressedThisFrame())
            {
                ToggleTooltip(cam);
            }
        }

        private bool RaycastHitsMe(Camera cam, Vector2 mouseScreen)
        {
            // Pixel-art pipeline: la cámara renderiza a un RT chiquito, así que
            // pixelWidth/Height ≠ Screen.width/Height. Escalamos el mouse pos al
            // viewport interno de la cámara antes del ScreenPointToRay. Mismo fix
            // que TileClickHandler usa para sus raycasts.
            var rtPos = new Vector2(
                mouseScreen.x / Screen.width  * cam.pixelWidth,
                mouseScreen.y / Screen.height * cam.pixelHeight);
            var ray = cam.ScreenPointToRay(rtPos);
            var hits = Physics.RaycastAll(ray, _raycastDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                var hitGo = hits[i].collider != null ? hits[i].collider.gameObject : null;
                if (hitGo == null) continue;
                if (hitGo == gameObject) return true;
                if (hitGo.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        private static bool TryGetMouseScreenPos(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                return true;
            }
            pos = Vector2.zero;
            return false;
#else
            pos = Input.mousePosition;
            return true;
#endif
        }

        private static bool MouseLeftPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        // Único lugar que mueve _hoverActive: el flanco tiene que ser uno solo para que el
        // evento no se dispare dos veces ni se saltee la salida.
        private void SetHover(bool on, Camera cam)
        {
            if (_hoverActive == on) return;
            _hoverActive = on;

            if (on) ShowTooltip(cam);
            else HideTooltip();

            HoverChanged?.Invoke(on);
        }

        private void ShowTooltip(Camera cam)
        {
            var content = BuildContent();

            // Con tarjetas o con banda el panel vale aunque el párrafo venga vacío: la columna
            // ES el contenido.
            if (content.IsEmpty) return;
            if (TooltipController.Instance == null) return;

            TooltipController.Instance.Show(
                content, ResolvePlacementScreenPos(cam), _ownerId, _placement.Mode);
        }

        private TooltipContent BuildContent()
        {
            var cards = CardsProvider?.Invoke();
            if (ContentProvider == null) return TooltipContent.FromText(ResolveText(), cards);

            // Las tarjetas siguen viniendo del CardsProvider aunque haya banda: las arma la fila
            // que flota sobre la cabeza, y son la MISMA lista que esa fila pinta.
            var content = ContentProvider.Invoke();
            return new TooltipContent(
                text: content.Text, name: content.Name, cards: cards, flavor: content.Flavor,
                health: content.Health, maxHealth: content.MaxHealth, shield: content.Shield);
        }

        private void ToggleTooltip(Camera cam)
        {
            string text = ResolveText();
            if (string.IsNullOrEmpty(text) || TooltipController.Instance == null) return;
            TooltipController.Instance.Toggle(text, ResolvePlacementScreenPos(cam), _ownerId, _placement.Mode);
        }

        // Punto-pantalla final según el modo: AutoFit ancla al objeto 3D (el controller
        // suma su offset global y clampea a pantalla); Fixed ancla al RectTransform de UI
        // configurado + offset en píxeles de referencia (resolución-independiente). Fixed
        // SIN anchor cae al objeto 3D + offset en píxeles reales — para tooltips de mundo
        // conviene configurar un anchor de UI o usar AutoFit.
        private Vector2 ResolvePlacementScreenPos(Camera cam)
        {
            if (_placement.Mode == TooltipPlacementMode.Fixed && _placement.FixedAnchor != null)
                return _placement.ResolveFixedScreenPos(null);

            Vector2 objScreen = ResolveAnchorScreenPos(cam);
            return _placement.Mode == TooltipPlacementMode.Fixed
                ? objScreen + _placement.FixedOffset
                : objScreen;
        }

        // WorldToScreenPoint devuelve coords del viewport interno de la cámara — para
        // pixel-art que renderiza a un RT chiquito, eso es del RT, no del Screen real.
        // Re-escalamos al Screen para que el TooltipController (Canvas Overlay) ancle
        // donde el user ve la puerta.
        private Vector2 ResolveAnchorScreenPos(Camera cam)
        {
            Vector3 rtPos = cam.WorldToScreenPoint(transform.position);
            if (cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return rtPos;
            return new Vector2(
                rtPos.x / cam.pixelWidth  * Screen.width,
                rtPos.y / cam.pixelHeight * Screen.height);
        }

        private void HideTooltip()
        {
            if (TooltipController.Instance != null) TooltipController.Instance.Hide(_ownerId);
        }

        // Levanta HoverChanged(false) a propósito: es lo que apaga el dibujo cuando el enemigo
        // muere con el mouse encima.
        private void OnDisable()
        {
            SetHover(false, null);
        }

        private string ResolveText()
        {
            if (TextProvider == null) TextProvider = TooltipResolver.AutoResolve(this);
            return TextProvider?.Invoke();
        }

#if UNITY_EDITOR
        [Title("Preview (solo editor)")]
        [TextArea(2, 5)]
        [Tooltip("Texto de ejemplo usado por el botón de preview — elegí uno del largo " +
                 "real esperado para ver cuánto espacio ocupa el panel.")]
        [SerializeField] private string _previewText =
            "<b>Forzar Puerta</b>\nCosto: 2 de energía\nPuntaje a superar: 25";

        [Button("Mostrar preview en Game view")]
        private void ShowEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogWarning("[WorldTooltipTrigger] No hay TooltipController en la escena.", this);
                return;
            }
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null && _placement.FixedAnchor == null)
            {
                Debug.LogWarning("[WorldTooltipTrigger] Sin cámara para proyectar el objeto — " +
                                 "asigná _camera o usá modo Fixed con anchor de UI.", this);
                return;
            }
            controller.EditorPreview(_previewText, ResolvePlacementScreenPos(cam), _placement.Mode);
        }

        [Button("Ocultar preview")]
        private void HideEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller != null) controller.EditorPreviewHide();
        }
#endif
    }
}
