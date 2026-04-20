using System;
using UnityEngine;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Placeholder AI handler that logs the enemy turn and does nothing else.
    /// Will be replaced by real AI logic in S#0012b.
    /// </summary>
    public sealed class StubEnemyAIHandler : IEnemyAIHandler
    {
        public void HandleEnemyTurn(Guid enemyId)
        {
            Debug.Log($"[StubEnemyAIHandler] Enemy turn for {enemyId} — no-op (stub).");
        }
    }
}
