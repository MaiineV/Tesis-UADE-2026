using Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Lee el action map <c>Camera</c> del proyecto y delega al
    /// <see cref="CameraService"/> adjunto. TECHNICAL.md §17.E.4.
    /// </summary>
    /// <remarks>
    /// El usuario autoriza el <c>Assets/InputSystem_Actions.inputactions</c> con
    /// el map <c>Camera</c> y enlaza el <c>InputActionAsset</c> de una de estas
    /// formas (por orden de precedencia):
    /// <list type="number">
    /// <item>Setea <see cref="Configure"/> antes de <c>OnEnable</c> (uso manual).</item>
    /// <item>Dropea el asset en el campo serializado <c>_actions</c>.</item>
    /// <item>Dropea el asset en <see cref="CameraServiceBootstrap"/> — el router
    /// lo resuelve via <see cref="ServiceLocator"/> en <c>OnEnable</c>.</item>
    /// </list>
    /// Sin ninguna de esas opciones, el router queda inerte y la cámara sólo
    /// responde a comandos directos al <see cref="ICameraService"/>.
    /// </remarks>
    [RequireComponent(typeof(CameraService))]
    [AddComponentMenu("Rollgeon/Camera/Camera Input Router")]
    public sealed class CameraInputRouter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _mapName = "Camera";

        private CameraService _service;

        private InputAction _rotateModifier;
        private InputAction _rotateDrag;
        private InputAction _panModifier;
        private InputAction _panDrag;
        private InputAction _zoom;
        private InputAction _recenter;
        private InputActionMap _map;

        private bool _rotateHeld;
        private bool _panHeld;

        private void Awake()
        {
            _service = GetComponent<CameraService>();
        }

        /// <summary>
        /// Wiring opcional desde <see cref="CameraServiceBootstrap"/>: permite
        /// asignar el <see cref="InputActionAsset"/> y el nombre del map en
        /// runtime. Si el diseñador ya los seteó en el inspector del
        /// componente, esta llamada es redundante pero inofensiva.
        /// </summary>
        public void Configure(InputActionAsset actions, string mapName)
        {
            _actions = actions;
            if (!string.IsNullOrEmpty(mapName)) _mapName = mapName;
        }

        private void OnEnable()
        {
            if (_actions == null && ServiceLocator.TryGetService<CameraInputConfig>(out var fromLocator))
            {
                _actions = fromLocator.Actions;
                if (!string.IsNullOrEmpty(fromLocator.MapName)) _mapName = fromLocator.MapName;
            }

            if (_actions == null) return;
            _map = _actions.FindActionMap(_mapName, throwIfNotFound: false);
            if (_map == null) return;

            _rotateModifier = _map.FindAction("RotateModifier", throwIfNotFound: false);
            _rotateDrag = _map.FindAction("RotateDrag", throwIfNotFound: false);
            _panModifier = _map.FindAction("PanModifier", throwIfNotFound: false);
            _panDrag = _map.FindAction("PanDrag", throwIfNotFound: false);
            _zoom = _map.FindAction("Zoom", throwIfNotFound: false);
            _recenter = _map.FindAction("Recenter", throwIfNotFound: false);

            if (_rotateModifier != null)
            {
                _rotateModifier.performed += OnRotateModifierPerformed;
                _rotateModifier.canceled += OnRotateModifierCanceled;
            }
            if (_rotateDrag != null) _rotateDrag.performed += OnRotateDrag;
            if (_panModifier != null)
            {
                _panModifier.performed += OnPanModifierPerformed;
                _panModifier.canceled += OnPanModifierCanceled;
            }
            if (_panDrag != null) _panDrag.performed += OnPanDrag;
            if (_zoom != null) _zoom.performed += OnZoom;
            if (_recenter != null) _recenter.performed += OnRecenter;

            _map.Enable();
        }

        private void OnDisable()
        {
            if (_map == null) return;

            if (_rotateModifier != null)
            {
                _rotateModifier.performed -= OnRotateModifierPerformed;
                _rotateModifier.canceled -= OnRotateModifierCanceled;
            }
            if (_rotateDrag != null) _rotateDrag.performed -= OnRotateDrag;
            if (_panModifier != null)
            {
                _panModifier.performed -= OnPanModifierPerformed;
                _panModifier.canceled -= OnPanModifierCanceled;
            }
            if (_panDrag != null) _panDrag.performed -= OnPanDrag;
            if (_zoom != null) _zoom.performed -= OnZoom;
            if (_recenter != null) _recenter.performed -= OnRecenter;

            _map.Disable();
        }

        private void OnRotateModifierPerformed(InputAction.CallbackContext _) => _rotateHeld = true;
        private void OnRotateModifierCanceled(InputAction.CallbackContext _)
        {
            _rotateHeld = false;
            _service.ResetRotationDrag();
        }
        private void OnRotateDrag(InputAction.CallbackContext ctx)
        {
            if (!_rotateHeld || _service == null) return;
            var delta = ctx.ReadValue<Vector2>();
            _service.AccumulateRotationDrag(delta.x);
        }

        private void OnPanModifierPerformed(InputAction.CallbackContext _) => _panHeld = true;
        private void OnPanModifierCanceled(InputAction.CallbackContext _) => _panHeld = false;
        private void OnPanDrag(InputAction.CallbackContext ctx)
        {
            if (!_panHeld || _service == null) return;
            _service.PanBy(ctx.ReadValue<Vector2>());
        }

        private void OnZoom(InputAction.CallbackContext ctx)
        {
            if (_service == null) return;
            float y = ctx.ReadValue<Vector2>().y;
            if (Mathf.Approximately(y, 0f)) return;
            _service.ZoomBy(Mathf.Sign(y));
        }

        private void OnRecenter(InputAction.CallbackContext _)
        {
            if (_service == null) return;
            _service.RecenterOnPlayer(instant: false);
        }
    }
}
