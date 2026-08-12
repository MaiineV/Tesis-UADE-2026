using Rollgeon.Items;

namespace Rollgeon.Chests
{
    /// <summary>
    /// API completa del sistema de cofres, para consola y tests. El runtime de
    /// combate consume solo <see cref="IChestRegistry"/>.
    /// </summary>
    public interface IChestService : IChestRegistry
    {
        /// <summary>Cofre activo de la sala actual, o <c>null</c>.</summary>
        ChestRuntime ActiveChest { get; }

        /// <summary>
        /// Spawnea un cofre en la sala actual ignorando el roll de frecuencia
        /// (dev console). Reemplaza al cofre activo si lo hay. No persiste en
        /// <c>ChestState</c> — es una herramienta de debug.
        /// </summary>
        bool DebugSpawn(ItemRarity tier, bool isMimic);
    }
}
