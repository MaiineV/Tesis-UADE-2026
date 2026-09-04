using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Capacidades de movimiento que Casillas Especiales y el pathing IA necesitan además
    /// de <see cref="IMovementService"/>. Interfaz aditiva a propósito: agregar miembros a
    /// <c>IMovementService</c> rompería sus fakes de tests; los consumidores resuelven esta
    /// vía cast (<c>movement is IPathedMovementService</c>) y degradan a <c>Move</c> si no está.
    /// </summary>
    public interface IPathedMovementService
    {
        /// <summary>
        /// Ejecuta un path explícito ya decidido por el caller (ruta del planner IA, segmento
        /// de deslizamiento, remanente de empuje). <c>path[0]</c> debe ser la posición actual;
        /// cada paso debe ser adyacente, walkable y libre. Dispara <c>OnEntityMoved</c> con
        /// ESTE path — no lo recalcula con A*.
        /// </summary>
        /// <param name="applyPathFilter">
        /// <c>true</c> = el path pasa por el <see cref="IMovementPathFilter"/> como un
        /// movimiento voluntario (rutas del planner IA). <c>false</c> (default) = commit crudo:
        /// el caller ya resolvió la semántica de tiles (segmentos del motor de casillas).
        /// </param>
        bool CommitPath(Guid entity, IReadOnlyList<GridCoord> path, bool applyPathFilter = false);

        /// <summary>
        /// Reubicación instantánea (portales). Mueve la ocupación y dispara
        /// <see cref="OnEntityTeleported"/> — NO <c>OnEntityMoved</c>: un teleport no "entra"
        /// a ninguna casilla intermedia ni a la de destino (spec: spawn/teleport nunca
        /// disparan OnEnter).
        /// </summary>
        bool Teleport(Guid entity, GridCoord to);

        /// <summary>
        /// Registra el filtro de path de movimiento voluntario. Uno solo (el motor de
        /// Casillas Especiales); <c>null</c> lo desengancha.
        /// </summary>
        void SetPathFilter(IMovementPathFilter filter);

        /// <summary>
        /// Anclas alcanzables para <paramref name="entity"/> respetando su footprint
        /// (Fase B: un 2×2 no entra por un pasillo de 1). Para un 1×1 el resultado y el
        /// ORDEN de descubrimiento son idénticos a <see cref="IMovementService.GetReachableTiles"/>
        /// — el scoring estricto de los nodos IA depende de ese orden. Default <c>null</c> =
        /// "no soportado": el caller degrada a <c>GetReachableTiles</c>.
        /// </summary>
        List<GridCoord> GetReachableAnchors(Guid entity, int range) => null;

        /// <summary>Args: (entity, from, to).</summary>
        event Action<Guid, GridCoord, GridCoord> OnEntityTeleported;

        /// <summary>
        /// Intercambia las posiciones de dos entidades ya registradas (Probability Drive: dos
        /// enemigos se cruzan). Dispara <see cref="OnEntityTeleported"/> para cada una — mismo
        /// evento que <see cref="Teleport"/>, no hay "entrada" a ninguna celda intermedia.
        /// Default member: <c>false</c> = "no soportado" para fakes de test existentes.
        /// </summary>
        /// <returns><c>false</c> si <paramref name="a"/> o <paramref name="b"/> no están registradas.</returns>
        bool Swap(Guid a, Guid b) => false;
    }
}
