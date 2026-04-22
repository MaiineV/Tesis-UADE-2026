using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Default in-memory implementation de <see cref="IEnemyAIRegistry"/>.
    /// </summary>
    public sealed class EnemyAIRegistry : IEnemyAIRegistry
    {
        private struct Entry
        {
            public AIDecisionNode Root;
            public int MaxHp;
        }

        private readonly Dictionary<Guid, Entry> _byId = new Dictionary<Guid, Entry>();

        public void Register(Guid enemyId, AIDecisionNode root, int maxHp)
        {
            if (enemyId == Guid.Empty)
                throw new ArgumentException("enemyId cannot be Guid.Empty", nameof(enemyId));
            _byId[enemyId] = new Entry { Root = root, MaxHp = maxHp };
        }

        public void Unregister(Guid enemyId) => _byId.Remove(enemyId);

        public bool TryGet(Guid enemyId, out AIDecisionNode root, out int maxHp)
        {
            if (_byId.TryGetValue(enemyId, out var e))
            {
                root = e.Root;
                maxHp = e.MaxHp;
                return true;
            }
            root = null;
            maxHp = 0;
            return false;
        }

        public bool Has(Guid enemyId) => _byId.ContainsKey(enemyId);
    }
}
