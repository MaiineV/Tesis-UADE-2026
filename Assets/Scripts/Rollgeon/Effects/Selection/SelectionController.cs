using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Effects.Selection
{
    public sealed class SelectionController : ISelectionController
    {
        // Estilo del highlight de path previewado durante hover. Configurado en
        // TileHighlightService default styles. Si se le pasa un estilo desconocido al
        // service, cae a amarillo — funciona pero no se distingue del "selected".
        private const string PathHighlightStyle = "path";

        private SelectionRequest _request;
        private List<TargetRef> _selected;
        private HashSet<GridCoord> _validCoords;

        // Cache del último coord hovered para evitar recomputar el A* cada frame cuando
        // el cursor está quieto. Null = sin hover (el mouse no está sobre un tile válido).
        private GridCoord? _lastHoveredCoord;
        private bool _hasPathPreview;

        public bool IsSelecting => _request != null;

        public event Action<TargetSelectionResult> OnSelectionCompleted;

        public void BeginSelection(SelectionRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _selected = new List<TargetRef>();
            _validCoords = new HashSet<GridCoord>();
            _lastHoveredCoord = null;
            _hasPathPreview = false;

            if (request.ValidTargets != null)
            {
                foreach (var t in request.ValidTargets)
                    _validCoords.Add(t.Coord);
            }

            UnityEngine.Debug.Log($"[SelectionController] BeginSelection — {_validCoords.Count} valid coords, style='{request.HighlightStyle}'");

            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                var style = request.HighlightStyle ?? "move";
                highlight.Highlight(_validCoords, style);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[SelectionController] ITileHighlightService not registered — no highlights");
            }
        }

        public void OnTargetHovered(TargetRef target)
        {
            if (_request == null) return;

            var coord = target?.Coord;
            if (Nullable.Equals(coord, _lastHoveredCoord)) return;
            _lastHoveredCoord = coord;

            // Solo hacemos path preview en selecciones de movimiento. El estilo "move" lo
            // configuran los HeroActionBehavior de Movement; otras selecciones (attack,
            // heal) usan estilos distintos y no tienen sentido como "camino A*".
            var style = _request.HighlightStyle ?? "move";
            if (style != "move" || !coord.HasValue || !_validCoords.Contains(coord.Value))
            {
                ClearPathPreview(style);
                return;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || !ServiceLocator.TryGetService<IMovementService>(out var movement)
                || !grid.TryGetPosition(_request.OwnerGuid, out var origin))
            {
                ClearPathPreview(style);
                return;
            }

            var path = movement.FindPath(origin, coord.Value);
            if (path == null || path.Count < 2)
            {
                ClearPathPreview(style);
                return;
            }

            // Repintamos el rango entero (sobrescribe colores previos del path), después
            // pintamos el path encima — es más simple que llevar tracking per-tile y el
            // SetPropertyBlock es barato.
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                highlight.Highlight(_validCoords, style);
                highlight.Highlight(path, PathHighlightStyle);
                _hasPathPreview = true;
            }
        }

        private void ClearPathPreview(string rangeStyle)
        {
            if (!_hasPathPreview) return;
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
                highlight.Highlight(_validCoords, rangeStyle);
            _hasPathPreview = false;
        }

        public void OnTargetClicked(TargetRef target)
        {
            if (_request == null || target == null)
            {
                UnityEngine.Debug.Log($"[SelectionController] OnTargetClicked ignored — request={_request != null} target={target != null}");
                return;
            }

            bool isValid = _validCoords.Contains(target.Coord);
            UnityEngine.Debug.Log($"[SelectionController] OnTargetClicked coord={target.Coord} valid={isValid}");

            if (!isValid) return;

            foreach (var s in _selected)
            {
                if (s.Coord == target.Coord)
                {
                    UnityEngine.Debug.Log($"[SelectionController] OnTargetClicked coord={target.Coord} SKIPPED (already selected)");
                    return;
                }
            }

            _selected.Add(target);

            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
                highlight.HighlightSingle(target.Coord, "selected");

            var settings = _request.Settings;
            if (settings != null && settings.AutoAccept)
            {
                int required = settings.GetSelectionCount(default);
                UnityEngine.Debug.Log($"[SelectionController] AutoAccept check — selected={_selected.Count} required={required}");
                if (_selected.Count >= required)
                    Complete();
            }
        }

        public void CancelSelection()
        {
            ClearHighlights();

            var result = new TargetSelectionResult
            {
                WasCancelled = true,
            };

            _request = null;
            _selected = null;
            _validCoords = null;
            _lastHoveredCoord = null;
            _hasPathPreview = false;

            OnSelectionCompleted?.Invoke(result);
        }

        private void Complete()
        {
            UnityEngine.Debug.Log($"[SelectionController] Complete — {_selected.Count} targets selected");
            ClearHighlights();

            var result = new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef>(_selected),
            };

            _request = null;
            _selected = null;
            _validCoords = null;
            _lastHoveredCoord = null;
            _hasPathPreview = false;

            OnSelectionCompleted?.Invoke(result);
        }

        private void ClearHighlights()
        {
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
                highlight.ClearAll();
        }
    }
}
