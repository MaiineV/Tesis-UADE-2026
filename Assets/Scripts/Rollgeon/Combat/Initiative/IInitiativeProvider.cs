using System;

namespace Rollgeon.Combat.Initiative
{
    /// <summary>
    /// Estrategia reemplazable que devuelve el valor de initiative de una
    /// entidad (TECHNICAL.md §12.7). Mayor = antes en la cola.
    /// </summary>
    /// <remarks>
    /// El default es <c>DefaultInitiativeProvider</c> — <c>Speed + die(Min, Max)</c>.
    /// Cualquier modo de juego que quiera reglas distintas (p.e. initiative
    /// plana, orden fijo, etc.) implementa esta interfaz y se registra en el
    /// bootstrap vía <c>ServiceLocator.AddService&lt;IInitiativeProvider&gt;(...)</c>.
    /// </remarks>
    public interface IInitiativeProvider
    {
        /// <summary>
        /// Devuelve el valor de initiative para ordenar el round. Mayor = antes.
        /// </summary>
        int RollInitiative(Guid entityGuid);
    }
}
