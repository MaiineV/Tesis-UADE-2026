using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Grid;

namespace Rollgeon.Dungeon.State
{
    /// <summary>
    /// Vuelca el estado <b>vivo</b> de los enemigos de una sala (HP actual, tile de
    /// grilla y GUID) a sus <see cref="EnemySpawnState"/>s (#0028). Necesario porque
    /// el HP mid-combate vive en el stat <c>Health</c> runtime y NO se escribe al
    /// <see cref="EnemySpawnState"/> salvo al huir por puerta (<c>EffForceDoor</c>).
    /// Se llama en <c>DungeonManager.CaptureState</c> para que un save con enemigos
    /// vivos reanude con su HP/posición exactos.
    /// <para>
    /// Pareo idéntico al de <c>EffForceDoor.HealCurrentRoomEnemies</c>:
    /// <see cref="RoomInstance.SpawnedEnemies"/> (vivos, en orden de spawn) contra los
    /// <see cref="EnemySpawnState"/> no-muertos ordenados por <c>SpawnPointIndex</c>.
    /// </para>
    /// </summary>
    public static class RoomEnemyStateSync
    {
        public static void SnapshotLiveEnemies(RoomInstance instance)
        {
            if (instance?.SpawnedEnemies == null || instance.SpawnedEnemies.Count == 0) return;

            ServiceLocator.TryGetService<AttributesManager>(out var attributes);
            ServiceLocator.TryGetService<IGridManager>(out var grid);

            var aliveStates = new List<EnemySpawnState>();
            foreach (var kv in instance.ObjectStates.Enumerate())
                if (kv.Value is EnemySpawnState s && !s.IsDead) aliveStates.Add(s);
            aliveStates.Sort((a, b) => a.SpawnPointIndex.CompareTo(b.SpawnPointIndex));

            int pairCount = Math.Min(instance.SpawnedEnemies.Count, aliveStates.Count);
            for (int i = 0; i < pairCount; i++)
            {
                var guid = instance.SpawnedEnemies[i];
                if (guid == Guid.Empty) continue;

                var state = aliveStates[i];
                state.Guid = guid.ToString();

                if (attributes != null)
                {
                    var health = attributes.GetAttribute<Health>(guid);
                    if (health != null) state.CurrentHP = health.Value;
                }

                if (grid != null && grid.TryGetPosition(guid, out var coord))
                {
                    state.LastCell = coord;
                    state.HasLastCell = true;
                }
            }
        }
    }
}
