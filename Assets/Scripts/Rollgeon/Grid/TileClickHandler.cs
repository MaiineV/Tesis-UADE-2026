using System;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.GameCamera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Grid
{
    [AddComponentMenu("Rollgeon/Grid/Tile Click Handler")]
    public sealed class TileClickHandler : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _tileLayer;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _mapName = "UI";

        private InputActionMap _map;
        private InputAction _clickAction;
        private InputAction _positionAction;

        // Cache del último coord hovered. Polling en Update evita pegar al SelectionController
        // por frame con coords idénticos — solo notifica cuando el cursor cruza un tile.
        // Tipo nullable: null = "el mouse no está sobre ningún tile válido".
        private GridCoord? _lastHoveredCoord;

        private void OnEnable()
        {
            if (_actions == null
                && ServiceLocator.TryGetService<CameraInputConfig>(out var inputCfg))
            {
                _actions = inputCfg.Actions;
            }

            if (_actions == null)
            {
                Debug.LogWarning("[TileClickHandler] InputActionAsset is null — clicks disabled", this);
                return;
            }

            _map = _actions.FindActionMap(_mapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogWarning($"[TileClickHandler] ActionMap '{_mapName}' not found in {_actions.name}", this);
                return;
            }

            _clickAction = _map.FindAction("Click", throwIfNotFound: false);
            _positionAction = _map.FindAction("Point", throwIfNotFound: false);

            Debug.Log($"[TileClickHandler] OnEnable — map='{_mapName}' click={_clickAction != null} point={_positionAction != null}", this);

            if (_clickAction != null)
                _clickAction.performed += OnClick;

            _map.Enable();
        }

        private void OnDisable()
        {
            if (_clickAction != null)
                _clickAction.performed -= OnClick;

            _map?.Disable();
            _lastHoveredCoord = null;
        }

        private void Update()
        {
            // Polling de hover: solo cuando hay una selección activa, para no quemar
            // raycasts cada frame en exploration libre. El raycast usa el mismo path
            // que OnClick (escalado RT→Screen incluido).
            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller)) return;
            if (!controller.IsSelecting)
            {
                if (_lastHoveredCoord != null)
                {
                    _lastHoveredCoord = null;
                    controller.OnTargetHovered(null);
                }
                return;
            }
            if (_positionAction == null) return;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            var screenPos = _positionAction.ReadValue<Vector2>();
            var rtPos = new Vector2(
                screenPos.x / Screen.width  * cam.pixelWidth,
                screenPos.y / Screen.height * cam.pixelHeight);

            var ray = cam.ScreenPointToRay(rtPos);
            GridCoord? hovered = null;
            if (Physics.Raycast(ray, out var hit, 100f, _tileLayer))
            {
                var marker = hit.collider.GetComponentInParent<TileMarker>();
                if (marker != null)
                {
                    hovered = marker.Coord;
                }
                else if (ServiceLocator.TryGetService<IGridManager>(out var grid))
                {
                    hovered = grid.WorldToGrid(hit.point);
                }
            }

            if (Nullable.Equals(hovered, _lastHoveredCoord)) return;
            _lastHoveredCoord = hovered;
            controller.OnTargetHovered(hovered.HasValue ? TargetRef.At(hovered.Value) : null);
        }

        private void OnClick(InputAction.CallbackContext ctx)
        {
            Debug.Log("[TileClickHandler] OnClick fired");

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller))
            {
                Debug.Log("[TileClickHandler] ISelectionController not registered");
                return;
            }
            if (!controller.IsSelecting)
            {
                Debug.Log("[TileClickHandler] Not selecting — ignoring click");
                return;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.Log("[TileClickHandler] IGridManager not registered");
                return;
            }

            var screenPos = _positionAction != null
                ? _positionAction.ReadValue<Vector2>()
                : Vector2.zero;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[TileClickHandler] No camera found");
                return;
            }

            // Si la cámara renderiza a un RenderTexture (pixel art pipeline), sus
            // dimensiones internas (pixelWidth/Height) difieren de las de pantalla.
            // Escalamos screenPos al espacio del RT antes de pasarlo al raycast.
            var rtPos = new Vector2(
                screenPos.x / Screen.width  * cam.pixelWidth,
                screenPos.y / Screen.height * cam.pixelHeight);

            Debug.Log($"[TileClickHandler] Raycast from screenPos={screenPos} rtPos={rtPos} layer={_tileLayer.value}");
            var ray = cam.ScreenPointToRay(rtPos);
            if (!Physics.Raycast(ray, out var hit, 100f, _tileLayer))
            {
                Debug.Log("[TileClickHandler] Raycast missed — no tile hit");
                return;
            }

            Debug.Log($"[TileClickHandler] Hit '{hit.collider.name}' at {hit.point}");
            var marker = hit.collider.GetComponentInParent<TileMarker>();
            if (marker != null)
            {
                Debug.Log($"[TileClickHandler] TileMarker found — coord={marker.Coord}");
                controller.OnTargetClicked(TargetRef.At(marker.Coord));
                return;
            }

            var coord = grid.WorldToGrid(hit.point);
            Debug.Log($"[TileClickHandler] No TileMarker — WorldToGrid={coord}");
            controller.OnTargetClicked(TargetRef.At(coord));
        }
    }
}
