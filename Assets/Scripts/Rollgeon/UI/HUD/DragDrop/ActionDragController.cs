using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Concretes;
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
        private string _activeStyle = "move";
        private bool _hasHover;
        private GridCoord _hoverCoord;

        private RectTransform _ghost;
        private ActionPlayDispatcher _dispatcher;

        public bool IsDragging => _current != null;

        // ---- Lifecycle / self-wiring ----------------------------------------

        private void OnEnable()
        {
            ResolveTileLayer();
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

                // CNF-002 v2: los chips de combate se activan SOLO por drag — el click
                // queda deshabilitado en cada botón que este controller gobierna.
                button.SetClickActivation(false);
            }
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
            _hasHover = false;

            // committed no dispara nada extra hoy; se deja el hook para feedback futuro
            // (SFX de drop aceptado / rechazado, tween de vuelta del ghost, etc.).
            _ = committed;
        }

        // ---- Commit ---------------------------------------------------------

        private bool TryCommit(ActionButton button, PointerEventData eventData)
        {
            // Drop sobre la HUD (cualquier UI con raycast) = cancelar. El raycast 3D
            // "atravesaría" la HUD hacia un tile detrás, así que gateamos antes.
            if (IsPointerOverUI(eventData)) return false;

            bool hitTile = TryRaycastCoord(eventData, out var coord);

            if (_requiresTile)
            {
                // Targeting por celda (Movement, ataques): sólo commitea si la celda
                // soltada está en el set válido.
                if (!hitTile || !ActionDragPolicy.IsValidDrop(coord, _validCoords))
                    return false;
            }
            // CNF-002 v2: acciones auto-target / instantáneas (Heal, Force Door) se activan
            // al soltar en CUALQUIER lado fuera de la UI — no requieren celda debajo. El
            // gate IsPointerOverUI de arriba ya cubre "soltar de vuelta sobre los chips".

            ResolveDispatcher()?.Commit(button, coord);
            return true;
        }

        // ---- Valid tiles / highlight ----------------------------------------

        // Mirror de PlayerSelectingSubState.Enter: busca el primer effect con selección
        // BeforeRoll y devuelve su SelectionSettings. Null si la acción no requiere selección
        // (self-cast, auto-resolve, etc.).
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
                    if (eff == null) continue;

                    // CNF-002: los ataques son EffChain. La selección real (Occupied+Enemies,
                    // BeforeRoll) vive en los effects de la phase 0 — el Selection propio del
                    // chain es un default vacío que igual reporta BeforeRoll, así que hay que
                    // mirar adentro (mismo criterio que CombatHandoffService.FindPhaseSelectionAt).
                    if (eff is EffChain chain)
                    {
                        var phase0 = chain.PhaseCount > 0 ? chain.Phases[0] : null;
                        if (phase0?.Effects?.Effects == null) continue;
                        foreach (var phaseEff in phase0.Effects.Effects)
                        {
                            if (phaseEff != null && phaseEff.RequiresSelectionAt(SelectionTiming.BeforeRoll))
                                return phaseEff.GetSelection();
                        }
                        continue;
                    }

                    if (eff.RequiresSelectionAt(SelectionTiming.BeforeRoll))
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

            // CNF-002: selecciones de enemigo se pintan rojas ("attack"), igual que el
            // highlight que abre el handoff al commitear. El resto usa el estilo default.
            _activeStyle = (_selection.EntityFilter & EntityFilterMask.Enemies) != 0
                ? "attack"
                : _highlightStyle;

            if (_validCoords.Count > 0
                && ServiceLocator.TryGetService<ITileHighlightService>(out var hl) && hl != null)
            {
                hl.Highlight(_validCoords, _activeStyle);
            }
        }

        private void ClearHighlights()
        {
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var hl) && hl != null)
                hl.ClearAll();
        }

        // ---- Raycast (mirror TileClickHandler) ------------------------------

        private bool TryRaycastCoord(PointerEventData eventData, out GridCoord coord, bool log = true)
        {
            coord = default;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return false;

            var rtPos = RenderTextureCursor.ScreenToRt(
                eventData.position, Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);
            var ray = cam.ScreenPointToRay(rtPos);

            // CNF-002 v2: el pawn manda sobre el piso — soltar SOBRE el modelo del enemigo
            // debe targetear SU celda y no la que queda DETRÁS con la cámara en ángulo.
            // Mismo criterio que TileClickHandler: ver PawnPicker.
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
                return false;

            var resolved = PawnPicker.ResolveCoord(ray, grid);
            if (resolved == null) return false;

            coord = resolved.Value;
            return true;
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

            // Snap: sobre una celda válida, el ghost se imanta al centro de la celda en vez
            // de seguir libre al puntero — lee mejor cuál celda va a recibir el drop.
            var screenPos = eventData.position;
            GridCoord hoverCoord = default;
            bool snapped = _requiresTile && TrySnapToValidCell(eventData, ref screenPos, out hoverCoord);
            if (_requiresTile) UpdateHoverHighlight(snapped, hoverCoord);

            var cam = GetCanvasCamera(_dragLayer);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer, screenPos, cam, out var local))
                _ghost.anchoredPosition = local;
        }

        // Devuelve true (y sobreescribe screenPos con el centro de la celda proyectado a
        // pantalla) si el puntero está sobre una celda válida del drag en curso.
        private bool TrySnapToValidCell(PointerEventData eventData, ref Vector2 screenPos, out GridCoord coord)
        {
            if (!TryRaycastCoord(eventData, out coord, log: false)) return false;
            if (!ActionDragPolicy.IsValidDrop(coord, _validCoords)) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return false;

            // WorldToScreenPoint devuelve píxeles del RT de la cámara — hay que
            // reescalar a pantalla (inversa del ScreenToRt que usa el picking).
            var rtPos = cam.WorldToScreenPoint(grid.GridToWorld(coord));
            if (rtPos.z <= 0f) return false;
            screenPos = RenderTextureCursor.RtToScreen(
                new Vector2(rtPos.x, rtPos.y), Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);
            return true;
        }

        // La celda snapeada se marca con el estilo "selected" (amarillo) por encima del set
        // base. Al cambiar/perder el hover se repinta el set completo con el estilo activo —
        // más barato que trackear el estilo previo por celda y no toca otros overlays.
        private void UpdateHoverHighlight(bool snapped, GridCoord coord)
        {
            if (snapped && _hasHover && coord.Equals(_hoverCoord)) return;
            if (!snapped && !_hasHover) return;

            _hasHover = snapped;
            _hoverCoord = coord;

            if (!ServiceLocator.TryGetService<ITileHighlightService>(out var hl) || hl == null) return;
            if (_validCoords.Count > 0) hl.Highlight(_validCoords, _activeStyle);
            if (snapped) hl.HighlightSingle(coord, "selected");
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
