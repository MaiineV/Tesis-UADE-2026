using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Hook de Casillas Especiales sobre el movimiento voluntario: recibe el path planeado
    /// y devuelve el path efectivo, truncado inclusive en la primera casilla que termina el
    /// movimiento para esa unidad (Hielo, Portal). El resto del recorrido lo continúa el
    /// motor de tiles como commits nuevos (slide / teleport), cada uno con su propio
    /// <c>OnEntityMoved</c>.
    /// </summary>
    /// <remarks>
    /// Invariante que protege este seam: <c>OnEntityMoved</c> solo anuncia casillas que la
    /// unidad realmente pisó. Sin el truncado, un path que cruza un portal reportaría
    /// entradas a casillas posteriores que la unidad nunca tocó.
    /// </remarks>
    public interface IMovementPathFilter
    {
        /// <summary>
        /// Path efectivo para <paramref name="entity"/>. Devolver el mismo path (o
        /// <c>null</c>) significa "sin cambios". El path incluye el origen en el índice 0.
        /// </summary>
        IReadOnlyList<GridCoord> Filter(Guid entity, IReadOnlyList<GridCoord> plannedPath);
    }
}
