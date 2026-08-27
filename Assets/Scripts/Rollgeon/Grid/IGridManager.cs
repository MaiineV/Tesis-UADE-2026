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
    /// <para>
    /// Footprint (Fase A): una entidad puede ocupar un rectángulo de celdas (ver
    /// <see cref="GridFootprint"/>). Su posición (<see cref="TryGetPosition"/>) sigue siendo
    /// una sola celda: el <b>ancla</b> (inferior-izquierda). Los miembros con implementación
    /// default tratan a todo como 1×1 para que las implementaciones existentes sigan
    /// compilando; <see cref="GridManager"/> los sobreescribe.
    /// </para>
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

        /// <summary>
        /// Registra una entidad 1×1 en el tile. Sobrescribe si ya estaba en otro tile y desaloja
        /// (con warning) a quien estuviera ahí. Sobre un guid ya registrado con footprint
        /// multi-celda, conserva ese footprint (equivale a <see cref="TryRegister"/>).
        /// </summary>
        void Register(Guid entityGuid, GridCoord coord);

        void Unregister(Guid entityGuid);

        /// <summary>Mueve <paramref name="entityGuid"/> a <paramref name="to"/>. Devuelve
        /// <c>false</c> si el destino está ocupado o fuera de bounds/walkable.</summary>
        bool Move(Guid entityGuid, GridCoord to);

        Vector3 GridToWorld(GridCoord c);
        GridCoord WorldToGrid(Vector3 world);

        /// <summary>Un par por entidad: guid → ancla.</summary>
        IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants();

        // ---- footprint (Fase A) ------------------------------------------

        /// <summary>Tamaño registrado de la entidad; (1,1) si no está registrada o es común.</summary>
        Vector2Int GetFootprint(Guid entityGuid) => GridFootprint.Unit;

        /// <summary>
        /// True si todas las celdas del rectángulo son walkable y están libres (o las ocupa
        /// <paramref name="ignore"/>, para mover una entidad sobre sí misma).
        /// </summary>
        bool CanPlace(GridCoord anchor, Vector2Int footprint, Guid ignore = default)
        {
            foreach (var c in GridFootprint.Cells(anchor, footprint))
            {
                if (!IsWalkable(c)) return false;
                if (TryGetOccupant(c, out var occupant) && occupant != ignore) return false;
            }
            return true;
        }

        /// <summary>
        /// Registra la entidad ocupando el rectángulo. A diferencia de <see cref="Register"/>,
        /// un footprint multi-celda <b>no desaloja</b>: devuelve <c>false</c> sin tocar nada si
        /// alguna celda está tomada o no es walkable. (1,1) equivale a <see cref="Register"/>.
        /// </summary>
        bool TryRegister(Guid entityGuid, GridCoord anchor, Vector2Int footprint)
        {
            if (!CanPlace(anchor, footprint, entityGuid)) return false;
            Register(entityGuid, anchor);
            return true;
        }

        /// <summary>Celdas que cubre la entidad (vacío si no está registrada).</summary>
        IEnumerable<GridCoord> OccupiedCells(Guid entityGuid)
            => TryGetPosition(entityGuid, out var anchor)
                ? GridFootprint.Cells(anchor, GetFootprint(entityGuid))
                : Array.Empty<GridCoord>();
    }
}
