using System;
using Rollgeon.Grid;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>
    /// Primitivo de movimiento forzado (empuje). La habilidad Empuje del Guerrero la consume
    /// vía <c>Rollgeon.Combat.Skills.Push.ClassSkillPushResolver</c> (que clasifica el choque
    /// con <see cref="ForcedMoveResult.BlockerGuid"/>) — acá vive solo la física de grilla: mover N celdas
    /// en una dirección, frenando en obstáculos, disparando los triggers de cada celda
    /// atravesada y resolviendo las continuaciones (deslizamiento de Hielo, teleport +
    /// remanente de Portal) en el orden del GDD §12.
    /// </summary>
    public interface IForcedMovementService
    {
        /// <summary>
        /// Empuja a <paramref name="entity"/> <paramref name="tiles"/> celdas hacia
        /// <paramref name="direction"/>. <paramref name="sourceId"/> = quién empuja
        /// (reservado para atribución; el daño de las casillas sale con el id de cada casilla).
        /// </summary>
        ForcedMoveResult Push(Guid entity, Cardinal direction, int tiles, Guid sourceId);
    }
}
