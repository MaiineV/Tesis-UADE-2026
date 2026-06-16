using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Implementación default de <see cref="IGridManager"/>. TECHNICAL.md §17.§I.
    /// </summary>
    public sealed class GridManager : IGridManager
    {
        private readonly Dictionary<Guid, GridCoord> _entityToCoord = new Dictionary<Guid, GridCoord>();
        private readonly Dictionary<GridCoord, Guid> _coordToEntity = new Dictionary<GridCoord, Guid>();

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
        }

        public bool InBounds(GridCoord c) => Graph.InBounds(c);

        public bool IsWalkable(GridCoord c) => Graph.HasNode(c);

        public bool IsOccupied(GridCoord c) => _coordToEntity.ContainsKey(c);

        public bool IsFree(GridCoord c) => IsWalkable(c) && !IsOccupied(c);

        public bool TryGetOccupant(GridCoord c, out Guid entityGuid) =>
            _coordToEntity.TryGetValue(c, out entityGuid);

        public bool TryGetPosition(Guid entityGuid, out GridCoord coord) =>
            _entityToCoord.TryGetValue(entityGuid, out coord);

        public void Register(Guid entityGuid, GridCoord coord)
        {
            if (entityGuid == Guid.Empty)
                throw new ArgumentException("Guid.Empty no puede registrarse", nameof(entityGuid));

            if (_entityToCoord.TryGetValue(entityGuid, out var prev))
            {
                _coordToEntity.Remove(prev);
            }

            if (_coordToEntity.TryGetValue(coord, out var occupant) && occupant != entityGuid)
            {
                Debug.LogWarning($"[GridManager] Register({entityGuid}) en {coord} — ya ocupado por {occupant}. Sobrescribiendo.");
                if (_entityToCoord.TryGetValue(occupant, out var occupantCoord) && occupantCoord == coord)
                {
                    _entityToCoord.Remove(occupant);
                }
            }

            _entityToCoord[entityGuid] = coord;
            _coordToEntity[coord] = entityGuid;
        }

        public void Unregister(Guid entityGuid)
        {
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
    }
}
