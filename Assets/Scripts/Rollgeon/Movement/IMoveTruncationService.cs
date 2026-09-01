using System;
using Rollgeon.Grid;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Reconciliación de un movimiento cancelado a mitad de la caminata visual:
    /// <see cref="IMovementService.Move"/> adelanta la posición lógica del grid al
    /// destino de forma sincrónica, así que al frenar el pawn en una celda
    /// intermedia hay que traer la posición lógica DE VUELTA a esa celda.
    /// </summary>
    /// <remarks>
    /// Interfaz aditiva separada (patrón <see cref="IPathedMovementService"/>) para
    /// no romper los fakes de <see cref="IMovementService"/> en tests; se resuelve
    /// por cast sobre la implementación concreta.
    /// </remarks>
    public interface IMoveTruncationService
    {
        /// <summary>
        /// Se emitió una truncación: <c>(entity, fromLógico, celdaFinal)</c>. NO se
        /// re-emite <see cref="IMovementService.OnEntityMoved"/> a propósito — los
        /// suscriptores de tiles/hazards ya procesaron el path completo y un
        /// evento de movimiento nuevo re-dispararía efectos de la celda de parada.
        /// </summary>
        event Action<Guid, GridCoord, GridCoord> OnEntityMoveTruncated;

        /// <summary>
        /// Mueve la posición lógica de <paramref name="entity"/> a
        /// <paramref name="cell"/> (la celda donde el pawn frenó). <c>true</c> si la
        /// entidad quedó registrada en esa celda; <c>false</c> si la celda no es
        /// walkable / está ocupada / la entidad no está registrada — en ese caso la
        /// posición lógica queda donde estaba y el resync visual (BUG-069) manda.
        /// </summary>
        bool TryTruncateMoveAt(Guid entity, GridCoord cell);
    }
}
