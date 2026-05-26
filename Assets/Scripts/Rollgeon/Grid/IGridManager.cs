using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// API de la grilla de la sala activa. TECHNICAL.md §17.§I.
    /// </summary>
    /// <remarks>
    /// Run-scope — se registra al cargar la sala y se limpia con <c>ClearScope(Run)</c>
    /// al terminar la run. Mantiene ocupancia (<c>GridCoord → Guid</c>) y permite traducción
    /// a coordenadas del mundo si el <see cref="GridOrigin"/> y <see cref="TileSize"/> están
    /// configurados por el bootstrap de la sala.
    /// </remarks>
    public interface IGridManager
    {
        NavGraph Graph { get; }

        /// <summary>Origen en world-space del tile (0,0). Default <see cref="Vector3.zero"/>.</summary>
        Vector3 GridOrigin { get; }

        /// <summary>Tamaño (world-space) de un tile cuadrado. Default 1.</summary>
        float TileSize { get; }

        /// <summary>Carga una sala nueva. Borra ocupancia previa.</summary>
        void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f);

        bool InBounds(GridCoord c);
        bool IsWalkable(GridCoord c);
        bool IsOccupied(GridCoord c);
        bool IsFree(GridCoord c);

        bool TryGetOccupant(GridCoord c, out Guid entityGuid);
        bool TryGetPosition(Guid entityGuid, out GridCoord coord);

        /// <summary>Registra una entidad en el tile. Sobrescribe si ya estaba en otro tile.</summary>
        void Register(Guid entityGuid, GridCoord coord);

        void Unregister(Guid entityGuid);

        /// <summary>Mueve <paramref name="entityGuid"/> a <paramref name="to"/>. Devuelve
        /// <c>false</c> si el destino está ocupado o fuera de bounds/walkable.</summary>
        bool Move(Guid entityGuid, GridCoord to);

        Vector3 GridToWorld(GridCoord c);
        GridCoord WorldToGrid(Vector3 world);

        IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants();
    }
}
