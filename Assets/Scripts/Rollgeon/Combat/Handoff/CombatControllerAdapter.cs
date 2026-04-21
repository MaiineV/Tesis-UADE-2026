using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Thin adapter that implements <see cref="ICombatStarter"/> and
    /// <see cref="ICombatSignaller"/> by forwarding to a real
    /// <see cref="CombatController"/>. Registered in
    /// <see cref="Patterns.ServiceLocator"/> by the bootstrap or by
    /// <see cref="CombatHandoffService.CreateAndRegister"/>.
    /// </summary>
    public sealed class CombatControllerAdapter : ICombatStarter, ICombatSignaller, IPlayerCombatActions
    {
        private readonly CombatController _controller;

        public CombatControllerAdapter(CombatController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public void StartCombat(
            Guid playerId,
            IReadOnlyList<Guid> participants,
            Guid roomInstanceId,
            Action<Guid> enemyActionHandler)
        {
            _controller.StartCombat(playerId, participants, roomInstanceId, enemyActionHandler);
        }

        public void SignalEnemyDone() => _controller.SendEnemyDone();

        public void SendPlayerAction() => _controller.SendPlayerAction();

        public void EndPlayerTurn() => _controller.EndPlayerTurn();
    }
}
