using System;
using Patterns;
using Rollgeon.Movement;
using Rollgeon.Player;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// <see cref="IMovementTraversalPolicy"/> del canal dados: el jugador atraviesa unidades
    /// mientras el dado de Movimiento lleve <see cref="CapEtherealMovement"/> (Paso etéreo).
    /// Registrado por <c>DiceEnchantmentBootstrap</c>; sin bag (fuera de run) no aplica.
    /// </summary>
    public sealed class EtherealMovementPolicy : IMovementTraversalPolicy
    {
        public bool CanPassThroughUnits(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return false;
            if (ps.PlayerGuid != entity) return false;
            return EnchantmentCapabilityQueries.PlayerSlotHasCapability<CapEtherealMovement>(
                EnchantmentSlotRef.MovementDieSlot);
        }
    }
}
