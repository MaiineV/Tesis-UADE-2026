using System;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Action slot for enemy AI turn execution. The handoff service passes
    /// <see cref="HandleEnemyTurn"/> as the <c>enemyActionHandler</c> delegate
    /// to <see cref="ICombatStarter.StartCombat"/>.
    /// </summary>
    public interface IEnemyAIHandler
    {
        void HandleEnemyTurn(Guid enemyId);
    }
}
