using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Default implementation of <see cref="IEnemySpawnResolver"/>. Rolls enemies
    /// from the room's <see cref="EnemyPoolSO"/>, creates runtime stats via
    /// <see cref="EnemyDataSO.CreateRuntimeStats"/>, and registers each spawn
    /// in both <see cref="InMemoryEntityRegistry"/> (combat initiative / turn
    /// order lookups) and <see cref="AttributesManager"/> (stat reads by
    /// <c>BasicEnemyAI</c> and damage pipelines).
    /// </summary>
    /// <remarks>
    /// Los dos registros comparten <see cref="ModifiableAttributes"/> por entidad.
    /// <see cref="InMemoryEntityRegistry"/> es un stub legado (ver su comment) y
    /// eventualmente se unifica con <see cref="AttributesManager"/>; hasta
    /// entonces este resolver mantiene ambos en sync.
    /// </remarks>
    public sealed class DefaultEnemySpawnResolver : IEnemySpawnResolver
    {
        private readonly InMemoryEntityRegistry _registry;
        private readonly AttributesManager _attributes;

        public DefaultEnemySpawnResolver(InMemoryEntityRegistry registry, AttributesManager attributes)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
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
                result.Add((id, enemyData));
            }

            return result;
        }
    }
}
