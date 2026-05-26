using System;
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

        [SerializeField] private WorldTooltipMode _mode = WorldTooltipMode.Click;

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
                if (hitMe && !_hoverActive)
                {
                    _hoverActive = true;
                    ShowTooltip(cam);
                }
                else if (!hitMe && _hoverActive)
                {
                    _hoverActive = false;
                    HideTooltip();
                }
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

        private void ShowTooltip(Camera cam)
        {
            string text = ResolveText();
            if (string.IsNullOrEmpty(text) || TooltipController.Instance == null) return;
            TooltipController.Instance.Show(text, ResolveAnchorScreenPos(cam), _ownerId);
        }

        private void ToggleTooltip(Camera cam)
        {
            string text = ResolveText();
            if (string.IsNullOrEmpty(text) || TooltipController.Instance == null) return;
            TooltipController.Instance.Toggle(text, ResolveAnchorScreenPos(cam), _ownerId);
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

        private void OnDisable()
        {
            _hoverActive = false;
            HideTooltip();
        }

        private string ResolveText()
        {
            if (TextProvider == null) TextProvider = TooltipResolver.AutoResolve(this);
            return TextProvider?.Invoke();
        }
    }
}
