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
        /// Camino BFS <paramref name="from"/> → <paramref name="to"/>. Devuelve lista
        /// incluyendo origen y destino si hay ruta; vacía si no.
        /// </summary>
        List<GridCoord> FindPath(GridCoord from, GridCoord to);

        /// <summary>
        /// Ejecuta el movimiento de <paramref name="entity"/> a <paramref name="destination"/>
        /// (si alcanzable). Devuelve <c>true</c> si se movió (incluyendo caso origen == destino).
        /// </summary>
        bool Move(Guid entity, GridCoord destination);

        /// <summary>
        /// Notifica cambios de posición. Args: (entity, from, to, path).
        /// </summary>
        event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;
    }
}
