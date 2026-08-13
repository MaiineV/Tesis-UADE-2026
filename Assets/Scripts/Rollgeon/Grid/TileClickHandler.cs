using System;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.GameCamera;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Rollgeon.Grid
{
    [AddComponentMenu("Rollgeon/Grid/Tile Click Handler")]
    public sealed class TileClickHandler : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _mapName = "UI";

        private InputActionMap _map;
        private InputAction _clickAction;
        private InputAction _positionAction;

        // Cache del último coord hovered. Polling en Update evita pegar al SelectionController
        // por frame con coords idénticos — solo notifica cuando el cursor cruza un tile.
        // Tipo nullable: null = "el mouse no está sobre ningún tile válido".
        private GridCoord? _lastHoveredCoord;

        // Cacheado en Update (fuera del event processing): IsPointerOverGameObject
        // dentro de un callback de InputAction loguea warning y lee el frame anterior
        // igual. Un click sobre UI (pantalla del altar, pause, HUD) NO debe atravesar
        // hacia los tiles — sin esto, clickear la pantalla movía al player.
        private bool _pointerOverUi;

        // El altar de encantamiento es un panel centrado (no BaseScreen, no full-screen
        // raycast blocker) — clickear FUERA del panel no cuenta como "pointer over UI",
        // así que sin este gate el click atraviesa hacia los tiles y mueve al player
        // con la pantalla abierta. Los eventos OnEnchantmentAltarActivated/Closed son
        // la única señal global de ese estado (mismo patrón que EnchantmentAltarInteractable).
        private bool _modalBlocked;

        private void OnEnable()
        {
            EventManager.Subscribe(EventName.OnEnchantmentAltarActivated, OnModalOpened);
            EventManager.Subscribe(EventName.OnEnchantmentAltarClosed, OnModalClosed);

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
            EventManager.UnSubscribe(EventName.OnEnchantmentAltarActivated, OnModalOpened);
            EventManager.UnSubscribe(EventName.OnEnchantmentAltarClosed, OnModalClosed);

            if (_clickAction != null)
                _clickAction.performed -= OnClick;

            _map?.Disable();
            _lastHoveredCoord = null;
            _modalBlocked = false;
        }

        private void OnModalOpened(params object[] args) => _modalBlocked = true;
        private void OnModalClosed(params object[] args) => _modalBlocked = false;

        private void Update()
        {
            _pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // Polling de hover: solo cuando hay una selección activa, para no quemar
            // raycasts cada frame en exploration libre. El raycast usa el mismo path
            // que OnClick (escalado RT→Screen incluido).
            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller)) return;
            if (!controller.IsSelecting || _pointerOverUi || _modalBlocked)
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
            var rtPos = RenderTextureCursor.ScreenToRt(
                screenPos, Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return;

            var ray = cam.ScreenPointToRay(rtPos);
            var hovered = ResolveCoordUnderCursor(ray, grid);

            if (Nullable.Equals(hovered, _lastHoveredCoord)) return;
            _lastHoveredCoord = hovered;
            controller.OnTargetHovered(hovered.HasValue ? TargetRef.At(hovered.Value) : null);
        }

        private void OnClick(InputAction.CallbackContext ctx)
        {
            Debug.Log("[TileClickHandler] OnClick fired");

            if (_pointerOverUi)
            {
                Debug.Log("[TileClickHandler] Pointer over UI — ignoring click");
                return;
            }
            if (_modalBlocked)
            {
                Debug.Log("[TileClickHandler] Modal open (enchantment altar) — ignoring click");
                return;
            }
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
            var rtPos = RenderTextureCursor.ScreenToRt(
                screenPos, Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);

            Debug.Log($"[TileClickHandler] Raycast from screenPos={screenPos} rtPos={rtPos}");
            var ray = cam.ScreenPointToRay(rtPos);

            var clicked = ResolveCoordUnderCursor(ray, grid);
            if (clicked == null)
            {
                Debug.Log("[TileClickHandler] No tile under cursor");
                return;
            }

            Debug.Log($"[TileClickHandler] Click coord={clicked.Value}");
            controller.OnTargetClicked(TargetRef.At(clicked.Value));
        }

        /// <summary>
        /// Resuelve la celda bajo el cursor: la del pawn apuntado (click/hover sobre el
        /// enemigo = su celda, CNF-002), si no la del piso. Ver <see cref="PawnPicker"/>.
        /// </summary>
        private GridCoord? ResolveCoordUnderCursor(Ray ray, IGridManager grid)
            => PawnPicker.ResolveCoord(ray, grid);
    }
}
