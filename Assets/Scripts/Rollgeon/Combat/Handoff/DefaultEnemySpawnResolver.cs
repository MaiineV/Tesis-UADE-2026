using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Default <see cref="IEnemySpawnResolver"/>. Rolls enemies desde
    /// <see cref="RoomSO.PossibleSetups"/> o <see cref="RoomSO.EnemyPool"/> y
    /// registra cada spawn en <see cref="InMemoryEntityRegistry"/>,
    /// <see cref="AttributesManager"/>, <see cref="IGridManager"/>,
    /// <see cref="IEntityVisualService"/> y (si existe)
    /// <see cref="IEnemyAIRegistry"/>. Trackea GUIDs en
    /// <see cref="RoomInstance.SpawnedEnemies"/> + <see cref="EnemySpawnState"/>s
    /// para persistencia de HP entre visitas.
    /// </summary>
    public sealed class DefaultEnemySpawnResolver : IEnemySpawnResolver
    {
        private const int CombatDefaultSpawnCount = 2;
        private const int BossDefaultSpawnCount = 1;

        private readonly InMemoryEntityRegistry _registry;
        private readonly AttributesManager _attributes;
        private readonly IEnemyAIRegistry _aiRegistry;
        private readonly IGridManager _grid;
        private readonly IEntityVisualService _visuals;

        public DefaultEnemySpawnResolver(
            InMemoryEntityRegistry registry,
            AttributesManager attributes,
            IEnemyAIRegistry aiRegistry = null,
            IGridManager grid = null,
            IEntityVisualService visuals = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _aiRegistry = aiRegistry;
            _grid = grid;
            _visuals = visuals;
        }

        public List<(Guid id, EnemyDataSO data)> Resolve(RoomInstance instance, System.Random rng)
        {
            var result = new List<(Guid id, EnemyDataSO data)>();
            if (instance?.Template == null) return result;
            if (instance.State == RoomState.Cleared) return result;

            var room = instance.Template;
            var layout = instance.SpawnedPrefab != null
                ? instance.SpawnedPrefab.GetComponent<RoomLayout>()
                : null;

            // 1. Re-entry: respawn solo vivos con HP guardado.
            var existingStates = CollectEnemyStates(instance);
            if (existingStates.Count > 0)
            {
                foreach (var state in existingStates)
                {
                    if (state.IsDead) continue;
                    var data = LookupEnemyData(room, state.EnemyDataSOId);
                    if (data == null) continue;

                    var id = RegisterEnemyFromState(data, state, layout);
                    if (id != Guid.Empty)
                    {
                        result.Add((id, data));
                        instance.SpawnedEnemies.Add(id);
                    }
                }
                return result;
            }

            // 2. Primer spawn de la sala.
            var plan = BuildSpawnPlan(room, rng);
            int spawnIndex = 0;
            foreach (var enemyData in plan)
            {
                if (enemyData == null) continue;

                var id = RegisterEnemy(enemyData, spawnIndex, layout);
                if (id != Guid.Empty)
                {
                    result.Add((id, enemyData));
                    instance.SpawnedEnemies.Add(id);

                    instance.ObjectStates.Set(EnemyStateKey(spawnIndex), new EnemySpawnState
                    {
                        SpawnPointId = EnemyStateKey(spawnIndex),
                        EnemyDataSOId = enemyData.EntityId,
                        CurrentHP = enemyData.BaseHP,
                        IsDead = false,
                        SpawnPointIndex = spawnIndex
                    });
                }
                spawnIndex++;
            }

            return result;
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private List<EnemyDataSO> BuildSpawnPlan(RoomSO room, System.Random rng)
        {
            if (room.PossibleSetups != null && room.PossibleSetups.Count > 0)
            {
                var setup = room.PossibleSetups[rng.Next(room.PossibleSetups.Count)];
                if (setup != null && setup.Slots != null && setup.Slots.Count > 0)
                {
                    var plan = new List<EnemyDataSO>(setup.Slots.Count);
                    foreach (var slot in setup.Slots)
                    {
                        plan.Add(slot.Enemy);
                    }
                    return plan;
                }
            }

            int defaultCount = room.Type == RoomType.Boss
                ? BossDefaultSpawnCount
                : CombatDefaultSpawnCount;

            if (room.EnemyPool == null) return new List<EnemyDataSO>();

            var rolled = room.EnemyPool.RollForSpawns(defaultCount, rng);
            var list = new List<EnemyDataSO>(defaultCount);
            foreach (var e in rolled) list.Add(e);
            return list;
        }

        private Guid RegisterEnemy(EnemyDataSO enemyData, int spawnIndex, RoomLayout layout)
        {
            var id = Guid.NewGuid();
            var attrs = enemyData.CreateRuntimeStats();
            _registry.Register(id, attrs);
            _attributes.Register(id, attrs);

            if (_aiRegistry != null)
            {
                var aiRoot = enemyData.CreateRuntimeAIRoot();
                _aiRegistry.Register(id, aiRoot, enemyData.BaseHP);
            }

            var coord = ResolveSpawnCoord(layout, spawnIndex);
            if (_grid != null) _grid.Register(id, coord);
            if (_visuals != null) _visuals.SpawnEnemy(id, enemyData, coord);

            return id;
        }

        private Guid RegisterEnemyFromState(
            EnemyDataSO enemyData, EnemySpawnState state, RoomLayout layout)
        {
            var id = RegisterEnemy(enemyData, state.SpawnPointIndex, layout);
            if (id == Guid.Empty) return id;

            var health = _attributes.GetAttribute<Rollgeon.Attributes.Stats.Health>(id);
            if (health != null)
            {
                health.Value = System.Math.Max(0, state.CurrentHP);
            }

            return id;
        }

        private static List<EnemySpawnState> CollectEnemyStates(RoomInstance instance)
        {
            var list = new List<EnemySpawnState>();
            foreach (var kv in instance.ObjectStates.Enumerate())
            {
                if (kv.Value is EnemySpawnState state) list.Add(state);
            }
            return list;
        }

        private static EnemyDataSO LookupEnemyData(RoomSO room, string entityId)
        {
            if (room.PossibleSetups != null)
            {
                foreach (var setup in room.PossibleSetups)
                {
                    if (setup?.Slots == null) continue;
                    foreach (var slot in setup.Slots)
                    {
                        if (slot.Enemy != null && slot.Enemy.EntityId == entityId) return slot.Enemy;
                    }
                }
            }

            if (room.EnemyPool != null && room.EnemyPool.Entries != null)
            {
                foreach (var entry in room.EnemyPool.Entries)
                {
                    if (entry.Item != null && entry.Item.EntityId == entityId) return entry.Item;
                }
            }

            return null;
        }

        private GridCoord ResolveSpawnCoord(RoomLayout layout, int index)
        {
            if (layout != null && layout.EnemySpawnPoints != null && layout.EnemySpawnPoints.Count > 0
                && _grid != null)
            {
                var spawnPoint = layout.EnemySpawnPoints[index % layout.EnemySpawnPoints.Count];
                if (spawnPoint != null)
                {
                    return _grid.WorldToGrid(spawnPoint.position);
                }
            }

            // Fallback: fila a +3 del origen con índice en Y. Cubre tests sin
            // prefab + samples SO-puros hasta que todos los rooms migren.
            return new GridCoord(3, index);
        }

        private static string EnemyStateKey(int index) => $"enemy_{index}";
    }
}
