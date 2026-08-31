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

        // Estilo de la casilla "frente a puerta" (Exploración). Color configurable via
        // TileHighlightServiceBootstrap (rojo por default).
        private const string DoorHighlightStyle = "door";

        // Estilo del underlay de rango de un ataque (todo el alcance). Se pinta DEBAJO de
        // los targets seleccionables. Configurable via TileHighlightServiceBootstrap.
        private const string RangeHighlightStyle = "range";

        // Estilo del área AoE previewada al hovear un ancla válida (TargetMode.Aoe).
        // Configurable via TileHighlightServiceBootstrap.
        private const string AoeHighlightStyle = "aoe";

        private SelectionRequest _request;
        private List<TargetRef> _selected;
        private HashSet<GridCoord> _validCoords;
        private HashSet<GridCoord> _doorCoords;

        // Rango geométrico completo del ataque (solo visual, NO clickeable). Se pinta
        // debajo de _validCoords. Vacío para movimiento/heal/puertas.
        private HashSet<GridCoord> _rangeCoords;

        // Cache del último coord hovered para evitar recomputar el A* cada frame cuando
        // el cursor está quieto. Null = sin hover (el mouse no está sobre un tile válido).
        private GridCoord? _lastHoveredCoord;
        private bool _hasPathPreview;

        // Si true, no se pinta el rango de fondo: solo el path verde (en hover) y las
        // puertas. Lo setea el movimiento de Exploración (siempre armado). Sin rango de
        // fondo que sobrescriba, el path anterior se limpia con ClearAll en cada cambio.
        private bool _suppressRange;

        // Si true, no se pinta NINGÚN tinte de piso (ni path en hover, ni "selected" al
        // clickear); solo las puertas. Movimiento de Exploración → click-to-move limpio.
        private bool _suppressPathPreview;

        public bool IsSelecting => _request != null;

        public bool CanOverlayHoverPreview => _request == null || _suppressRange;

        public event Action<TargetSelectionResult> OnSelectionCompleted;

        public void RefreshHighlights()
        {
            if (_request == null) return;
            if (!ServiceLocator.TryGetService<ITileHighlightService>(out var highlight)) return;

            if (!_suppressRange)
            {
                RepaintRange(highlight);
                highlight.Highlight(_validCoords, _request.HighlightStyle ?? "move");
            }
            RepaintDoors(highlight);

            // El path preview quedó borrado por el ClearAll ajeno; se recomputa en el
            // próximo hover de tile.
            _hasPathPreview = false;
            _lastHoveredCoord = null;
        }

        public void BeginSelection(SelectionRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _selected = new List<TargetRef>();
            _validCoords = new HashSet<GridCoord>();
            _doorCoords = new HashSet<GridCoord>();
            _rangeCoords = new HashSet<GridCoord>();
            _lastHoveredCoord = null;
            _hasPathPreview = false;
            _suppressRange = request.SuppressRangeHighlight;
            _suppressPathPreview = request.SuppressPathPreview;

            if (request.ValidTargets != null)
            {
                foreach (var t in request.ValidTargets)
                    _validCoords.Add(t.Coord);
            }

            // El rango es solo visual: NO se agrega a _validCoords, así que clickear una
            // casilla del rango sin target sigue siendo no-op (OnTargetClicked lo filtra).
            if (request.RangeTiles != null)
            {
                foreach (var c in request.RangeTiles)
                    _rangeCoords.Add(c);
            }

            // Las casillas de puerta son clickeables aunque queden fuera del rango de
            // movimiento normal — el viejo click-to-pass tampoco chequeaba adyacencia.
            if (request.DoorTiles != null)
            {
                foreach (var d in request.DoorTiles)
                {
                    _doorCoords.Add(d);
                    _validCoords.Add(d);
                }
            }

            UnityEngine.Debug.Log($"[SelectionController] BeginSelection — {_validCoords.Count} valid coords ({_doorCoords.Count} door), style='{request.HighlightStyle}'");

            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                // Con _suppressRange no se pinta el rango: el player ve la sala limpia y
                // solo aparece el path verde al apuntar una casilla. Las puertas sí.
                if (!_suppressRange)
                {
                    // Primero el underlay del rango completo (ataques), luego los targets
                    // seleccionables por encima con su color propio.
                    RepaintRange(highlight);
                    highlight.Highlight(_validCoords, request.HighlightStyle ?? "move");
                }
                RepaintDoors(highlight);
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

            // Movimiento de Exploración: click-to-move limpio, sin ningún tinte que siga
            // al cursor. No calculamos ni pintamos path (las puertas ya viven aparte).
            if (_suppressPathPreview) return;

            // Solo hacemos path preview en selecciones de movimiento. El estilo "move" lo
            // configuran los HeroActionBehavior de Movement; otras selecciones (attack,
            // heal) usan estilos distintos y no tienen sentido como "camino A*".
            var style = _request.HighlightStyle ?? "move";

            // Preview del área AoE: al apuntar un ancla válida se pinta el área que sería
            // afectada. El área puede exceder el rango pintado (clipea a grilla, no al
            // Range del caster), así que tanto el repintado como la limpieza hacen
            // ClearAll — repintar solo _validCoords dejaría celdas del tinte AoE pegadas
            // fuera del rango.
            var settings = _request.Settings;
            if (settings != null && settings.TargetMode == TargetMode.Aoe)
            {
                if (!coord.HasValue || !_validCoords.Contains(coord.Value))
                {
                    ClearAoePreview(style);
                    return;
                }

                if (ServiceLocator.TryGetService<ITileHighlightService>(out var hl))
                {
                    hl.ClearAll();
                    if (!_suppressRange)
                    {
                        RepaintRange(hl);
                        hl.Highlight(_validCoords, style);
                    }

                    var area = settings.ExpandAoe(coord.Value, _request.OwnerGuid);
                    var areaCoords = new List<GridCoord>(area.Count);
                    foreach (var t in area) areaCoords.Add(t.Coord);
                    hl.Highlight(areaCoords, AoeHighlightStyle);

                    RepaintDoors(hl);
                    _hasPathPreview = true; // mismo flag: "hay overlay de hover que limpiar"
                }
                return;
            }

            // La casilla "frente a puerta" también previewa camino: el héroe CAMINA
            // hasta ella antes de cruzar (CrossDoorAfterArrival), así que el A* aplica
            // igual que en un move normal. RepaintDoors la mantiene roja por encima.
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
                // Sin rango de fondo, el path verde anterior no se sobrescribe solo —
                // hay que limpiarlo con ClearAll antes de pintar el nuevo. Con rango, el
                // repintado del rango ya tapa el path viejo (más barato que ClearAll).
                if (_suppressRange)
                    highlight.ClearAll();
                else
                    highlight.Highlight(_validCoords, style);
                highlight.Highlight(path, PathHighlightStyle);
                RepaintDoors(highlight); // las puertas quedan rojas por encima del rango/path
                _hasPathPreview = true;
            }
        }

        // Limpia el overlay AoE de un hover anterior. A diferencia de ClearPathPreview,
        // SIEMPRE hace ClearAll: el área AoE puede exceder _rangeCoords/_validCoords y
        // repintar solo esos sets dejaría celdas naranjas pegadas.
        private void ClearAoePreview(string rangeStyle)
        {
            if (!_hasPathPreview) return;
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                highlight.ClearAll();
                if (!_suppressRange)
                {
                    RepaintRange(highlight);
                    highlight.Highlight(_validCoords, rangeStyle);
                }
                RepaintDoors(highlight);
            }
            _hasPathPreview = false;
        }

        private void ClearPathPreview(string rangeStyle)
        {
            if (!_hasPathPreview) return;
            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                if (_suppressRange)
                    highlight.ClearAll(); // borra el path verde; no hay rango que repintar
                else
                    highlight.Highlight(_validCoords, rangeStyle);
                RepaintDoors(highlight);
            }
            _hasPathPreview = false;
        }

        // Repinta las casillas de puerta con su estilo rojo. Se llama después de cada
        // repintado del rango "move" (que las dejaría azules al estar en _validCoords).
        private void RepaintDoors(ITileHighlightService highlight)
        {
            if (_doorCoords != null && _doorCoords.Count > 0)
                highlight.Highlight(_doorCoords, DoorHighlightStyle);
        }

        // Pinta el underlay del rango completo (ataques). Se llama ANTES de _validCoords
        // para que los targets seleccionables queden con su color por encima.
        private void RepaintRange(ITileHighlightService highlight)
        {
            if (_rangeCoords != null && _rangeCoords.Count > 0)
                highlight.Highlight(_rangeCoords, _request?.RangeHighlightStyle ?? RangeHighlightStyle);
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

            // Dedupe por coord y por OCUPANTE (Fase C): dos celdas de un mismo footprint
            // multi-celda son el mismo target — el jugador no puede gastar dos picks en él.
            ServiceLocator.TryGetService<Grid.IGridManager>(out var pickGrid);
            Guid targetOccupant = Guid.Empty;
            pickGrid?.TryGetOccupant(target.Coord, out targetOccupant);
            foreach (var s in _selected)
            {
                bool sameCoord = s.Coord == target.Coord;
                bool sameOccupant = targetOccupant != Guid.Empty && pickGrid != null
                    && pickGrid.TryGetOccupant(s.Coord, out var prevOccupant)
                    && prevOccupant == targetOccupant;
                if (sameCoord || sameOccupant)
                {
                    UnityEngine.Debug.Log($"[SelectionController] OnTargetClicked coord={target.Coord} SKIPPED (already selected)");
                    return;
                }
            }

            _selected.Add(target);

            // En movimiento de Exploración no queremos ningún tinte de piso: saltamos el
            // "selected" para que el destino no quede pintado mientras el pawn camina.
            if (!_suppressPathPreview
                && ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                // Un ocupante multi-celda se pinta entero; celda vacía o 1×1, como siempre.
                if (targetOccupant != Guid.Empty && pickGrid != null)
                    highlight.Highlight(pickGrid.OccupiedCells(targetOccupant), "selected");
                else
                    highlight.HighlightSingle(target.Coord, "selected");
            }

            var settings = _request.Settings;
            if (settings != null && settings.AutoAccept)
            {
                int required = settings.GetSelectionCount(new ReadInfo { ownerGuid = _request.OwnerGuid });
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
            _doorCoords = null;
            _rangeCoords = null;
            _lastHoveredCoord = null;
            _hasPathPreview = false;

            OnSelectionCompleted?.Invoke(result);
        }

        private void Complete()
        {
            UnityEngine.Debug.Log($"[SelectionController] Complete — {_selected.Count} targets selected");
            ClearHighlights();

            // Único punto de expansión AoE del flujo manual: los consumidores (FSM, chain,
            // exploración, drag-drop) reciben SelectedTargets ya expandidos (ancla + área
            // filtrada contra la grilla viva) sin cambios en su código.
            var settings = _request?.Settings;
            var selected = settings != null
                           && settings.TargetMode == TargetMode.Aoe
                           && _selected.Count > 0
                ? settings.ExpandAoe(_selected[0].Coord, _request.OwnerGuid)
                : new List<TargetRef>(_selected);

            var result = new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = selected,
            };

            _request = null;
            _selected = null;
            _validCoords = null;
            _doorCoords = null;
            _rangeCoords = null;
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
