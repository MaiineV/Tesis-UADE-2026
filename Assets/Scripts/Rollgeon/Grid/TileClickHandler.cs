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

            Debug.Log($"[TileClickHandler] Raycast from screenPos={screenPos} layer={_tileLayer.value}");
            var ray = cam.ScreenPointToRay(screenPos);
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
