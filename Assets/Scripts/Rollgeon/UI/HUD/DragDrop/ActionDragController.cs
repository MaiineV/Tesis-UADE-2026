using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DragDrop
{
    /// <summary>
    /// Controlador central del drag-and-drop de acciones de combate. Espejo de Bot-Game
    /// <c>CardDragControllerUI</c>: mueve un "ghost" que sigue el cursor sobre un drag layer,
    /// resalta las celdas válidas de la acción arrastrada, y al soltar sobre una celda válida
    /// delega el commit al <see cref="ActionPlayDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// El picking reusa el mismo mecanismo que <see cref="TileClickHandler"/>: raycast a la capa
    /// tile con el cursor escalado al RenderTexture (<see cref="RenderTextureCursor"/>), resolviendo
    /// <see cref="TileMarker.Coord"/> (fallback <see cref="IGridManager.WorldToGrid"/>). El highlight
    /// reusa <see cref="ITileHighlightService"/>. No abre ninguna selección real durante el drag —
    /// sólo computa las celdas válidas read-only con <see cref="SelectionSettings.ResolveValidTiles"/>;
    /// la selección real la abre el pipeline de combate cuando el dispatcher hace el commit.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Drag Controller")]
    public sealed class ActionDragController : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Tooltip("Cámara que renderiza el combate. Fallback: Camera.main.")]
        private Camera _camera;

        [SerializeField, Tooltip("Capa de los tiles — la misma que usa TileClickHandler (layer 6).")]
        private LayerMask _tileLayer;

        [SerializeField, Tooltip("RectTransform (overlay, sort order alto) donde vive el ghost " +
                 "mientras se arrastra. Si es null se crea uno bajo el primer Canvas al arrancar.")]
        private RectTransform _dragLayer;

        [SerializeField, Tooltip("Si true, al habilitarse agrega un ActionDragHandle a cada ActionButton " +
                 "que no lo tenga. Deja el wiring en un solo componente (este).")]
        private bool _autoAttachHandles = true;

        [SerializeField, Tooltip("Raíz opcional bajo la que buscar los ActionButton para auto-attach. " +
                 "Null = busca en toda la escena.")]
        private Transform _buttonsRoot;

        [Title("Ghost")]
        [SerializeField, Tooltip("Estilo de highlight para las celdas válidas del drag (ver TileHighlightService).")]
        private string _highlightStyle = "move";

        [SerializeField, Range(0f, 1f), Tooltip("Alpha del ghost mientras se arrastra.")]
        private float _ghostAlpha = 0.85f;

        [SerializeField, Tooltip("Multiplicador de escala del ghost respecto del botón original.")]
        private float _ghostScale = 1f;

        // ---- Runtime state --------------------------------------------------

        private ActionButton _current;
        private SelectionSettings _selection;
        private bool _requiresTile;
        private readonly HashSet<GridCoord> _validCoords = new HashSet<GridCoord>();

        private RectTransform _ghost;
        private ActionPlayDispatcher _dispatcher;

        public bool IsDragging => _current != null;

        // ---- Lifecycle / self-wiring ----------------------------------------

        private void OnEnable()
        {
            ResolveTileLayer();
            Debug.Log($"[DragDrop] Controller OnEnable — autoAttach={_autoAttachHandles} tileLayer={_tileLayer.value} active={isActiveAndEnabled}");
            if (_autoAttachHandles) AttachHandles();
        }

        // Si el LayerMask quedó sin setear en el inspector, resolvemos "Tile" (layer 6,
        // el mismo que usa TileClickHandler) para que el picking funcione sin wiring manual.
        private void ResolveTileLayer()
        {
            if (_tileLayer.value != 0) return;
            int mask = LayerMask.GetMask("Tile");
            _tileLayer = mask != 0 ? mask : (1 << 6);
        }

        /// <summary>Agrega un <see cref="ActionDragHandle"/> a cada <see cref="ActionButton"/> que
        /// no lo tenga. Idempotente. Deja todo el wiring del feature en este único componente.</summary>
        public void AttachHandles()
        {
            var buttons = _buttonsRoot != null
                ? _buttonsRoot.GetComponentsInChildren<ActionButton>(includeInactive: true)
                : FindObjectsByType<ActionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int attached = 0;
            foreach (var button in buttons)
            {
                if (button == null) continue;
                if (button.GetComponent<ActionDragHandle>() == null)
                {
                    button.gameObject.AddComponent<ActionDragHandle>();
                    attached++;
                }
            }
            Debug.Log($"[DragDrop] AttachHandles — buttons encontrados={buttons.Length} handles nuevos={attached}");
        }

        // ---- Public API (llamada por ActionDragHandle) ----------------------

        /// <summary>Arranca un drag para <paramref name="button"/>. Devuelve <c>false</c> si ya
        /// hay un drag en curso.</summary>
        public bool BeginDrag(ActionButton button, PointerEventData eventData)
        {
            if (_current != null || button == null) return false;
            _current = button;
            _validCoords.Clear();

            _selection = ResolveBeforeRollSelection(button);
            _requiresTile = ActionDragPolicy.RequiresTileDrop(_selection);

            if (_requiresTile)
                ComputeAndHighlightValidTiles();

            CreateGhost(button, eventData);
            Debug.Log($"[DragDrop] BeginDrag ok slot={button.Slot} requiresTile={_requiresTile} " +
                      $"selection={(_selection != null)} validCoords={_validCoords.Count} ghost={(_ghost != null)}");
            return true;
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (_current == null) return;
            MoveGhost(eventData);
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (_current == null) return;

            var button = _current;
            _current = null;

            bool committed = TryCommit(button, eventData);

            ClearHighlights();
            DestroyGhost();
            _selection = null;
            _requiresTile = false;
            _validCoords.Clear();

            // committed no dispara nada extra hoy; se deja el hook para feedback futuro
            // (SFX de drop aceptado / rechazado, tween de vuelta del ghost, etc.).
            _ = committed;
        }

        // ---- Commit ---------------------------------------------------------

        private bool TryCommit(ActionButton button, PointerEventData eventData)
        {
            // Drop sobre la HUD (cualquier UI con raycast) = cancelar. El raycast 3D
            // "atravesaría" la HUD hacia un tile detrás, así que gateamos antes.
            if (IsPointerOverUI(eventData))
            {
                Debug.Log("[DragDrop] TryCommit: cancelado — pointer sobre UI (IsPointerOverGameObject).");
                return false;
            }

            bool hitTile = TryRaycastCoord(eventData, out var coord);
            Debug.Log($"[DragDrop] TryCommit hitTile={hitTile} coord={coord} requiresTile={_requiresTile} " +
                      $"isValid={(hitTile && ActionDragPolicy.IsValidDrop(coord, _validCoords))} validCoords={_validCoords.Count}");

            if (_requiresTile)
            {
                // Movement: sólo commitea si la celda soltada está en el set válido.
                if (!hitTile || !ActionDragPolicy.IsValidDrop(coord, _validCoords))
                    return false;
            }
            else
            {
                // Acciones auto-target / instantáneas: soltar sobre el tablero (cualquier tile)
                // commitea. Sin tile debajo (void off-grid) = cancelar.
                if (!hitTile)
                    return false;
            }

            Debug.Log($"[DragDrop] TryCommit: commit → dispatcher.Commit(coord={coord})");
            ResolveDispatcher()?.Commit(button, coord);
            return true;
        }

        // ---- Valid tiles / highlight ----------------------------------------

        // Mirror de PlayerSelectingSubState.Enter: busca el primer effect con selección
        // BeforeRoll y devuelve su SelectionSettings. Null si la acción no requiere selección
        // (ataques auto-target, self-cast, etc.).
        private SelectionSettings ResolveBeforeRollSelection(ActionButton button)
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps?.CurrentHero == null)
                return null;

            var behavior = ps.CurrentHero.ResolveBaseBehavior(button.Slot, GamePhase.Combat);
            if (behavior?.Effects == null) return null;

            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff != null && eff.RequiresSelectionAt(SelectionTiming.BeforeRoll))
                        return eff.GetSelection();
                }
            }
            return null;
        }

        private void ComputeAndHighlightValidTiles()
        {
            if (_selection == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!grid.TryGetPosition(ps.PlayerGuid, out var ownerPos)) return;

            var tiles = _selection.ResolveValidTiles(ownerPos, ps.PlayerGuid);
            if (tiles != null)
                foreach (var t in tiles)
                    if (t != null) _validCoords.Add(t.Coord);

            if (_validCoords.Count > 0
                && ServiceLocator.TryGetService<ITileHighlightService>(out var hl) && hl != null)
                hl.Highlight(_validCoords, _highlightStyle);
        }

        private void ClearHighlights()
        {
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var hl) && hl != null)
                hl.ClearAll();
        }

        // ---- Raycast (mirror TileClickHandler) ------------------------------

        private bool TryRaycastCoord(PointerEventData eventData, out GridCoord coord)
        {
            coord = default;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return false;

            var rtPos = RenderTextureCursor.ScreenToRt(
                eventData.position, Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);
            var ray = cam.ScreenPointToRay(rtPos);

            if (!Physics.Raycast(ray, out var hit, 100f, _tileLayer))
                return false;

            var marker = hit.collider.GetComponentInParent<TileMarker>();
            if (marker != null)
            {
                coord = marker.Coord;
                return true;
            }

            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                coord = grid.WorldToGrid(hit.point);
                return true;
            }
            return false;
        }

        // ---- Ghost ----------------------------------------------------------

        private void CreateGhost(ActionButton button, PointerEventData eventData)
        {
            EnsureDragLayer();
            if (_dragLayer == null) return;

            var go = new GameObject("ActionDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _ghost = go.GetComponent<RectTransform>();
            _ghost.SetParent(_dragLayer, false);
            _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(0.5f, 0.5f);

            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = _ghostAlpha;

            var ghostImg = go.GetComponent<Image>();
            ghostImg.raycastTarget = false;

            var srcImg = button.Button != null
                ? button.Button.targetGraphic as Image
                : button.GetComponentInChildren<Image>();
            if (srcImg != null)
            {
                ghostImg.sprite = srcImg.sprite;
                ghostImg.color = srcImg.color;
                ghostImg.preserveAspect = srcImg.preserveAspect;
            }

            var srcRect = button.transform as RectTransform;
            _ghost.sizeDelta = srcRect != null ? srcRect.rect.size : new Vector2(96f, 96f);
            _ghost.localScale = Vector3.one * _ghostScale;

            MoveGhost(eventData);
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (_ghost == null || _dragLayer == null) return;

            var cam = GetCanvasCamera(_dragLayer);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer, eventData.position, cam, out var local))
                _ghost.anchoredPosition = local;
        }

        private void DestroyGhost()
        {
            if (_ghost == null) return;
            Destroy(_ghost.gameObject);
            _ghost = null;
        }

        private void EnsureDragLayer()
        {
            if (_dragLayer != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("ActionDragLayer", typeof(RectTransform), typeof(CanvasGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            rt.SetAsLastSibling();
            _dragLayer = rt;
        }

        private static Camera GetCanvasCamera(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        // ---- Helpers --------------------------------------------------------

        private static bool IsPointerOverUI(PointerEventData eventData)
        {
            return EventSystem.current != null
                   && EventSystem.current.IsPointerOverGameObject(eventData.pointerId);
        }

        private ActionPlayDispatcher ResolveDispatcher()
        {
            if (_dispatcher != null) return _dispatcher;
            _dispatcher = GetComponent<ActionPlayDispatcher>();
            if (_dispatcher == null) _dispatcher = gameObject.AddComponent<ActionPlayDispatcher>();
            return _dispatcher;
        }
    }
}
