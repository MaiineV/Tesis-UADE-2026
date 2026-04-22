using System;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Registro Run-scope que mapea Guid → metadata de AI (árbol + MaxHp de referencia).
    /// TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// El <see cref="Rollgeon.Combat.Handoff.IEnemySpawnResolver"/> registra entries al
    /// spawn de cada enemigo. <see cref="TreeDrivenEnemyAI"/> los consume en cada turno.
    /// </remarks>
    public interface IEnemyAIRegistry
    {
        void Register(Guid enemyId, AIDecisionNode root, int maxHp);
        void Unregister(Guid enemyId);
        bool TryGet(Guid enemyId, out AIDecisionNode root, out int maxHp);
        bool Has(Guid enemyId);
    }
}
