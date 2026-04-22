using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Default implementation of <see cref="IEnemySpawnResolver"/>. Rolls enemies
    /// from the room's <see cref="EnemyPoolSO"/>, creates runtime stats via
    /// <see cref="EnemyDataSO.CreateRuntimeStats"/>, and registers each spawn
    /// in <see cref="InMemoryEntityRegistry"/>, <see cref="AttributesManager"/>
    /// y (si existe) <see cref="IEnemyAIRegistry"/> con el árbol clonado.
    /// </summary>
    /// <remarks>
    /// Los dos registros de attrs comparten <see cref="ModifiableAttributes"/> por
    /// entidad. <see cref="InMemoryEntityRegistry"/> es un stub legado y
    /// eventualmente se unifica con <see cref="AttributesManager"/>. El registry
    /// de AI es opcional — si no está inyectado, el árbol no se registra y el
    /// <see cref="TreeDrivenEnemyAI"/> cae a <see cref="BasicEnemyAI"/>.
    /// </remarks>
    public sealed class DefaultEnemySpawnResolver : IEnemySpawnResolver
    {
        private readonly InMemoryEntityRegistry _registry;
        private readonly AttributesManager _attributes;
        private readonly IEnemyAIRegistry _aiRegistry;

        public DefaultEnemySpawnResolver(
            InMemoryEntityRegistry registry,
            AttributesManager attributes,
            IEnemyAIRegistry aiRegistry = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _aiRegistry = aiRegistry;
        }

        public List<(Guid id, EnemyDataSO data)> Resolve(RoomSO room, int spawnCount, System.Random rng)
        {
            var result = new List<(Guid id, EnemyDataSO data)>();

            if (room == null) return result;
            if (room.EnemyPool == null) return result;
            if (spawnCount <= 0) return result;

            var rolled = room.EnemyPool.RollForSpawns(spawnCount, rng);

            foreach (var enemyData in rolled)
            {
                if (enemyData == null) continue;

                var id = Guid.NewGuid();
                var attrs = enemyData.CreateRuntimeStats();
                _registry.Register(id, attrs);
                _attributes.Register(id, attrs);

                if (_aiRegistry != null)
                {
                    var aiRoot = enemyData.CreateRuntimeAIRoot();
                    _aiRegistry.Register(id, aiRoot, enemyData.BaseHP);
                }

                result.Add((id, enemyData));
            }

            return result;
        }
    }
}
