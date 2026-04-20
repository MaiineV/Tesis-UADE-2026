using System;
using System.Collections.Generic;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Default implementation of <see cref="IEnemySpawnResolver"/>. Rolls enemies
    /// from the room's <see cref="EnemyPoolSO"/>, creates runtime stats via
    /// <see cref="EnemyDataSO.CreateRuntimeStats"/>, and registers each spawn
    /// in the <see cref="IEntityRegistry"/>.
    /// </summary>
    public sealed class DefaultEnemySpawnResolver : IEnemySpawnResolver
    {
        private readonly IEntityRegistry _registry;

        public DefaultEnemySpawnResolver(IEntityRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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
                result.Add((id, enemyData));
            }

            return result;
        }
    }
}
