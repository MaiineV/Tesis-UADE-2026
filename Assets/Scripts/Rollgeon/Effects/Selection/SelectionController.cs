using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    public sealed class SelectionController : ISelectionController
    {
        private SelectionRequest _request;
        private List<TargetRef> _selected;
        private HashSet<GridCoord> _validCoords;

        public bool IsSelecting => _request != null;

        public event Action<TargetSelectionResult> OnSelectionCompleted;

        public void BeginSelection(SelectionRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _selected = new List<TargetRef>();
            _validCoords = new HashSet<GridCoord>();

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

            _selected.Add(target);

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

            OnSelectionCompleted?.Invoke(result);
        }

        private void ClearHighlights()
        {
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
                highlight.ClearAll();
        }
    }
}
