using System;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Superficie mínima de consulta "¿este guid es un cofre?". La consumen
    /// <c>EntityQueryService</c> (clasifica cofres como <c>Props</c> para que la IA
    /// enemiga nunca los targetee), <c>CombatDeathWatcher</c> (la muerte de un cofre
    /// no es una muerte de enemigo) y <c>AINode_ExecuteTelegraph</c> (daño AoE
    /// incidental). Separada de <see cref="IChestService"/> para que los consumidores
    /// de combate no dependan de la API completa del cofre.
    /// </summary>
    public interface IChestRegistry
    {
        bool IsChest(Guid guid);

        /// <summary>Cofre activo en la sala actual. <c>false</c> si no hay ninguno.</summary>
        bool TryGetActiveChest(out Guid chestGuid);
    }
}
