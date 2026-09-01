using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Implementación default de <see cref="IGridManager"/>. TECHNICAL.md §17.§I.
    /// </summary>
    /// <remarks>
    /// <c>_entityToCoord</c> guarda el <b>ancla</b> de cada entidad; <c>_coordToEntity</c> tiene
    /// una entrada por celda cubierta (muchas → una para footprints multi-celda). Un 1×1 pasa
    /// por el mismo código de siempre, evicción con warning incluida.
    /// </remarks>
    public sealed class GridManager : IGridManager
    {
        private readonly Dictionary<Guid, GridCoord> _entityToCoord = new Dictionary<Guid, GridCoord>();
        private readonly Dictionary<GridCoord, Guid> _coordToEntity = new Dictionary<GridCoord, Guid>();
        // Solo entidades ≠ 1×1: ausente = unidad.
        private readonly Dictionary<Guid, Vector2Int> _entityToFootprint = new Dictionary<Guid, Vector2Int>();

        public NavGraph Graph { get; private set; } = new NavGraph();
        public Vector3 GridOrigin { get; private set; } = Vector3.zero;
        public float TileSize { get; private set; } = 1f;

        public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f)
        {
            Graph = graph ?? new NavGraph();
            GridOrigin = origin;
            TileSize = tileSize <= 0f ? 1f : tileSize;
            _entityToCoord.Clear();
            _coordToEntity.Clear();
            _entityToFootprint.Clear();
        }

        public bool InBounds(GridCoord c) => Graph.InBounds(c);

        public bool IsWalkable(GridCoord c) => Graph.HasNode(c);

        public bool IsOccupied(GridCoord c) => _coordToEntity.ContainsKey(c);

        public bool IsFree(GridCoord c) => IsWalkable(c) && !IsOccupied(c);

        public bool TryGetOccupant(GridCoord c, out Guid entityGuid) =>
            _coordToEntity.TryGetValue(c, out entityGuid);

        public bool TryGetPosition(Guid entityGuid, out GridCoord coord) =>
            _entityToCoord.TryGetValue(entityGuid, out coord);

        public Vector2Int GetFootprint(Guid entityGuid) =>
            _entityToFootprint.TryGetValue(entityGuid, out var fp) ? fp : GridFootprint.Unit;

        public IEnumerable<GridCoord> OccupiedCells(Guid entityGuid)
            => _entityToCoord.TryGetValue(entityGuid, out var anchor)
                ? GridFootprint.Cells(anchor, GetFootprint(entityGuid))
                : Array.Empty<GridCoord>();

        public bool CanPlace(GridCoord anchor, Vector2Int footprint, Guid ignore = default)
        {
            foreach (var c in GridFootprint.Cells(anchor, footprint))
            {
                if (!IsWalkable(c)) return false;
                if (_coordToEntity.TryGetValue(c, out var occupant) && occupant != ignore) return false;
            }
            return true;
        }

        public bool OccupiesAny(Guid entityGuid, Func<GridCoord, bool> area)
        {
            if (area == null) return false;
            foreach (var c in OccupiedCells(entityGuid))
                if (area(c)) return true;
            return false;
        }

        public List<Guid> DistinctOccupants(IEnumerable<GridCoord> coords)
        {
            var result = new List<Guid>();
            if (coords == null) return result;
            var seen = new HashSet<Guid>();
            foreach (var c in coords)
            {
                if (!_coordToEntity.TryGetValue(c, out var occupant) || occupant == Guid.Empty) continue;
                if (seen.Add(occupant)) result.Add(occupant);
            }
            return result;
        }

        public void Register(Guid entityGuid, GridCoord coord)
        {
            if (entityGuid == Guid.Empty)
                throw new ArgumentException("Guid.Empty no puede registrarse", nameof(entityGuid));

            // Un guid multi-celda no se encoge por un Register viejo: conserva su footprint.
            if (_entityToFootprint.TryGetValue(entityGuid, out var fp))
            {
                if (!TryRegister(entityGuid, coord, fp))
                    Debug.LogError($"[GridManager] Register({entityGuid}) en {coord}: el footprint {fp.x}×{fp.y} no cabe. Sin cambios.");
                return;
            }

            if (_entityToCoord.TryGetValue(entityGuid, out var prev))
            {
                _coordToEntity.Remove(prev);
            }

            if (_coordToEntity.TryGetValue(coord, out var occupant) && occupant != entityGuid)
            {
                Debug.LogWarning($"[GridManager] Register({entityGuid}) en {coord} — ya ocupado por {occupant}. Sobrescribiendo.");
                if (_entityToFootprint.ContainsKey(occupant))
                {
                    // Desalojar una celda de un rectángulo lo dejaría a medias: se va entero.
                    RemoveCells(occupant);
                }
                else if (_entityToCoord.TryGetValue(occupant, out var occupantCoord) && occupantCoord == coord)
                {
                    _entityToCoord.Remove(occupant);
                }
            }

            _entityToCoord[entityGuid] = coord;
            _coordToEntity[coord] = entityGuid;
        }

        public bool TryRegister(Guid entityGuid, GridCoord anchor, Vector2Int footprint)
        {
            footprint = GridFootprint.Normalize(footprint);
            if (GridFootprint.IsUnit(footprint))
            {
                // Volver a 1×1 desde un footprint mayor es explícito: se libera el rectángulo.
                if (_entityToFootprint.ContainsKey(entityGuid)) RemoveCells(entityGuid);
                Register(entityGuid, anchor);
                return true;
            }

            if (entityGuid == Guid.Empty)
                throw new ArgumentException("Guid.Empty no puede registrarse", nameof(entityGuid));

            if (!CanPlace(anchor, footprint, entityGuid)) return false;

            RemoveCells(entityGuid);
            _entityToCoord[entityGuid] = anchor;
            _entityToFootprint[entityGuid] = footprint;
            foreach (var c in GridFootprint.Cells(anchor, footprint)) _coordToEntity[c] = entityGuid;
            return true;
        }

        public void Unregister(Guid entityGuid)
        {
            if (_entityToFootprint.ContainsKey(entityGuid))
            {
                RemoveCells(entityGuid);
                return;
            }

            if (_entityToCoord.TryGetValue(entityGuid, out var coord))
            {
                _entityToCoord.Remove(entityGuid);
                if (_coordToEntity.TryGetValue(coord, out var occupant) && occupant == entityGuid)
                {
                    _coordToEntity.Remove(coord);
                }
            }
        }

        public bool Move(Guid entityGuid, GridCoord to)
        {
            if (!_entityToCoord.ContainsKey(entityGuid))
            {
                Debug.LogWarning($"[GridManager] Move: guid {entityGuid} no estaba registrado.");
                return false;
            }

            if (_entityToFootprint.TryGetValue(entityGuid, out var fp))
            {
                // El rectángulo entero tiene que caber; pisarse a sí mismo está permitido.
                return TryRegister(entityGuid, to, fp);
            }

            if (!IsWalkable(to)) return false;
            if (_coordToEntity.TryGetValue(to, out var occupant) && occupant != entityGuid)
            {
                return false;
            }

            Register(entityGuid, to);
            return true;
        }

        // Las entities se ubican en el CENTRO de la casilla (+0.5 tile en X/Z), no en
        // su esquina: con los tiles nuevos el pivot de cada tile quedó en la esquina,
        // así que sin el medio-tile los pawns aparecen corridos respecto de la grilla.
        public Vector3 GridToWorld(GridCoord c) =>
            GridOrigin + new Vector3((c.X + 0.5f) * TileSize, 0f, (c.Y + 0.5f) * TileSize);

        public GridCoord WorldToGrid(Vector3 world)
        {
            // FloorToInt (no RoundToInt) para ser la inversa exacta del centro de casilla:
            // un punto en cualquier parte de la celda [c, c+1) mapea a c.
            var local = world - GridOrigin;
            int x = Mathf.FloorToInt(local.x / TileSize);
            int y = Mathf.FloorToInt(local.z / TileSize);
            return new GridCoord(x, y);
        }

        public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => _entityToCoord;

        /// <summary>Libera todas las celdas de la entidad (ancla, footprint y cada celda cubierta).</summary>
        private void RemoveCells(Guid entityGuid)
        {
            if (!_entityToCoord.TryGetValue(entityGuid, out var anchor)) return;
            foreach (var c in GridFootprint.Cells(anchor, GetFootprint(entityGuid)))
            {
                if (_coordToEntity.TryGetValue(c, out var occupant) && occupant == entityGuid)
                    _coordToEntity.Remove(c);
            }
            _entityToCoord.Remove(entityGuid);
            _entityToFootprint.Remove(entityGuid);
        }
    }
}
