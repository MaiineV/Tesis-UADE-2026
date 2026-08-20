using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Registry + lifecycle + motor de triggers de las Casillas Especiales. Convive con
    /// <c>IHazardService</c> (hazards de bosses) sin tocarlo: ambos escuchan los mismos
    /// eventos pero administran instancias propias.
    /// </summary>
    public interface ISpecialTileService
    {
        /// <summary>
        /// Coloca una instancia (autoría de sala / payload de telegraph). Devuelve el
        /// instanceId, o <see cref="Guid.Empty"/> con definición/coords inválidas.
        /// </summary>
        Guid Place(SpecialTileDefinitionSO definition, IEnumerable<GridCoord> coords,
            TilePlacementOptions options = default);

        /// <summary>
        /// Crea una casilla temporal en runtime (jefes, ítems, eventos, habilidades) con
        /// los obligatorios del GDD: owner, duración, y validación de casilla libre —
        /// rechaza coords con otra casilla especial, con unidad, o no transitables.
        /// </summary>
        Guid CreateRuntime(SpecialTileDefinitionSO definition, GridCoord coord,
            RuntimeTileRequest request, out TilePlacementError error);

        /// <summary>Instancia activa que ocupa <paramref name="coord"/>, si hay una.</summary>
        bool TryGetTileAt(GridCoord coord, out SpecialTileInfo info);

        /// <summary>Snapshot materializado de todas las instancias activas.</summary>
        IEnumerable<SpecialTileInfo> ActiveInstances();

        /// <summary>Remueve una instancia y dispara <c>OnSpecialTileExpired</c>.</summary>
        void Remove(Guid instanceId);

        /// <summary>
        /// Reubica una instancia (Zona de Seguridad Móvil). La protección no se corta:
        /// los checks de "está adentro" son on-demand contra las coords vigentes.
        /// </summary>
        void MoveInstance(Guid instanceId, IEnumerable<GridCoord> newCoords);

        /// <summary>
        /// Resuelve las entradas de una unidad a una secuencia de celdas — el único camino
        /// por el que disparan los triggers de entrada. El movimiento voluntario llega solo
        /// (vía <c>OnEntityMoved</c>); el movimiento forzado lo llama explícito con
        /// <see cref="TileMovementKind.Forced"/>.
        /// </summary>
        void ResolveEntries(Guid entity, IReadOnlyList<GridCoord> enteredCoords, TileMovementKind kind);

        /// <summary>Fast-path para el pathing IA: sin instancias, el planner delega al scoring legacy.</summary>
        bool HasAnySpecialTiles { get; }
    }
}
