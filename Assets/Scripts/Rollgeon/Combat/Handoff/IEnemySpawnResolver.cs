using System;
using System.Collections.Generic;
using Rollgeon.Dungeon;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Resolves enemy spawns para un encounter. Devuelve pares
    /// <c>(runtime Guid, source data)</c> que el caller (handoff / combat
    /// starter) usa para armar el participant list. TECHNICAL.md §13.6.
    /// <para>
    /// Prefiere <see cref="RoomSO.PossibleSetups"/> (set pre-diseñado random)
    /// y cae a <see cref="RoomSO.EnemyPool"/>. Registra cada spawn en
    /// <see cref="RoomInstance.SpawnedEnemies"/> y seeds un
    /// <c>EnemySpawnState</c> en <see cref="RoomInstance.ObjectStates"/> para
    /// persistencia de HP entre visitas.
    /// </para>
    /// </summary>
    public interface IEnemySpawnResolver
    {
        List<(Guid id, EnemyDataSO data)> Resolve(RoomInstance instance, System.Random rng);
    }
}
