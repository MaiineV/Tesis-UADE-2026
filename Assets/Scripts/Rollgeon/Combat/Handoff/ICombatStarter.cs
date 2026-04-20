using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Abstraction over <see cref="FSM.CombatController.StartCombat"/> for
    /// testability. Production code uses <see cref="CombatControllerAdapter"/>;
    /// tests inject a spy/stub.
    /// </summary>
    public interface ICombatStarter
    {
        void StartCombat(
            Guid playerId,
            IReadOnlyList<Guid> participants,
            Guid roomInstanceId,
            Action<Guid> enemyActionHandler);
    }
}
