using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Pathfinding + ejecución de movimientos en la grilla activa. TECHNICAL.md §17.§B.
    /// </summary>
    /// <remarks>
    /// Run-scope. Usa <see cref="IGridManager"/> como fuente de verdad de walkable/ocupancia.
    /// La impl default hace BFS sobre 4-neighborhood. Para FP la ejecución del movimiento
    /// actualiza la grilla lógicamente y dispara <see cref="OnEntityMoved"/> — la capa visual
    /// (Worktree C) se suscribe y anima el GameObject correspondiente.
    /// </remarks>
    public interface IMovementService
    {
        /// <summary>
        /// Tiles alcanzables desde <paramref name="origin"/> en <paramref name="range"/>
        /// pasos, respetando walkable y ocupancia (excepto el origen). Incluye el origen
        /// si <paramref name="includeOrigin"/> es <c>true</c>.
        /// </summary>
        List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false);

        /// <summary>
        /// Igual que <see cref="GetReachableTiles"/> pero para una entidad concreta: aplica la
        /// <see cref="IMovementTraversalPolicy"/> (Paso etéreo atraviesa unidades). Default
        /// interface member para los fakes; la impl real lo override.
        /// </summary>
        List<GridCoord> GetReachableTilesFor(Guid entity, GridCoord origin, int range, bool includeOrigin = false)
            => GetReachableTiles(origin, range, includeOrigin);

        /// <summary>
        /// Camino BFS <paramref name="from"/> → <paramref name="to"/>. Devuelve lista
        /// incluyendo origen y destino si hay ruta; vacía si no.
        /// </summary>
        List<GridCoord> FindPath(GridCoord from, GridCoord to);

        /// <summary>
        /// Igual que <see cref="FindPath"/> pero para una entidad concreta: aplica la
        /// <see cref="IMovementTraversalPolicy"/> (Paso etéreo) y el footprint, igual que el
        /// camino que <see cref="TryMove"/> termina caminando. Es el path que la UI debe
        /// previsualizar. Default interface member para los fakes; la impl real lo override.
        /// </summary>
        List<GridCoord> FindPathFor(Guid entity, GridCoord from, GridCoord to) => FindPath(from, to);

        /// <summary>
        /// <c>true</c> si <paramref name="entity"/> atraviesa unidades como paso intermedio
        /// (Paso etéreo). La capa visual lo consulta para no re-rutear al pisar una celda
        /// ocupada que el path autorizó. Default <c>false</c>.
        /// </summary>
        bool CanPassThroughUnits(Guid entity) => false;

        /// <summary>
        /// Ejecuta el movimiento de <paramref name="entity"/> a <paramref name="destination"/>
        /// (si alcanzable). Devuelve <c>true</c> si se movió (incluyendo caso origen == destino).
        /// </summary>
        bool Move(Guid entity, GridCoord destination);

        /// <summary>
        /// Igual que <see cref="Move"/> pero devuelve el path efectivamente caminado
        /// (índice 0 = origen, post-filtro de path). <c>null</c> si no se movió. Default
        /// interface member para que los fakes de tests no cambien; la impl real lo override.
        /// </summary>
        bool TryMove(Guid entity, GridCoord destination, out IReadOnlyList<GridCoord> walkedPath)
        {
            walkedPath = null;
            return Move(entity, destination);
        }

        /// <summary>
        /// Notifica cambios de posición. Args: (entity, from, to, path).
        /// </summary>
        event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;
    }
}
