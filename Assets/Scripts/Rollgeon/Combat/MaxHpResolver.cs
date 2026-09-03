using System;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Player;

namespace Rollgeon.Combat
{
    /// <summary>
    /// Vida máxima de cualquier combatiente: enemigos vía <see cref="IEnemyAIRegistry"/>,
    /// el jugador vía <see cref="PlayerMaxHp"/> (incluye grants in-run). Centralizado para
    /// que efectos y preconditions (clamp de curación, umbral de Ejecutor) compartan la
    /// misma resolución.
    /// </summary>
    public static class MaxHpResolver
    {
        /// <summary>Devuelve <c>int.MaxValue</c> cuando no se puede resolver (no clampea nada).</summary>
        public static int Resolve(Guid target)
        {
            if (ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry)
                && aiRegistry != null
                && aiRegistry.TryGet(target, out _, out var maxHp)
                && maxHp > 0)
            {
                return maxHp;
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var players)
                && players != null
                && players.PlayerGuid == target)
            {
                int resolved = PlayerMaxHp.Resolve(target);
                if (resolved > 0) return resolved;
            }

            return int.MaxValue;
        }

        /// <summary><c>true</c> si se pudo resolver una vida máxima real.</summary>
        public static bool TryResolve(Guid target, out int maxHp)
        {
            maxHp = Resolve(target);
            return maxHp != int.MaxValue && maxHp > 0;
        }
    }
}
