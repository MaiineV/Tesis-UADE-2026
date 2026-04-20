using System;
using System.Collections.Generic;
using Rollgeon.Dungeon;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Resolves which enemies spawn for a given room encounter.
    /// Returns a list of (runtime Guid, source data) pairs so callers can
    /// register them in the entity registry and build participant lists.
    /// </summary>
    public interface IEnemySpawnResolver
    {
        List<(Guid id, EnemyDataSO data)> Resolve(RoomSO room, int spawnCount, System.Random rng);
    }
}
