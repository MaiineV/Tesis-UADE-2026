using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Economy;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
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
        private readonly EnemyGoldDropService _goldDrops;

        public DefaultEnemySpawnResolver(
            InMemoryEntityRegistry registry,
            AttributesManager attributes,
            IEnemyAIRegistry aiRegistry = null,
            IGridManager grid = null,
            IEntityVisualService visuals = null,
            EnemyGoldDropService goldDrops = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _aiRegistry = aiRegistry;
            _grid = grid;
            _visuals = visuals;
            _goldDrops = goldDrops;
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
                // En re-entry, los enemigos vivos reaparecen en posiciones aleatorias —
                // no en sus spawn points originales — para que la sala no se sienta
                // estática al volver. Excluimos tiles de puerta (no caer encima) y los
                // ya ocupados (player + otros enemigos del batch). Si no hay grid (tests
                // sin layout) o no hay candidatos válidos, caemos al spawn point legacy.
                var forbidden = CollectDoorCoords(layout);
                foreach (var state in existingStates)
                {
                    if (state.IsDead) continue;
                    var data = LookupEnemyData(room, state.EnemyDataSOId);
                    if (data == null) continue;

                    var randomCoord = TryPickRandomSpawnCoord(forbidden, rng);
                    var id = randomCoord.HasValue
                        ? RegisterEnemyAtCoord(data, randomCoord.Value, rng, state)
                        : RegisterEnemyFromState(data, state, layout, rng);

                    if (id != Guid.Empty)
                    {
                        result.Add((id, data));
                        instance.SpawnedEnemies.Add(id);
                    }
                }
                return result;
            }

            // 2. Primer spawn de la sala.
            var plan = BuildSpawnPlan(room, layout, rng);
            int spawnIndex = 0;
            foreach (var enemyData in plan)
            {
                if (enemyData == null) continue;

                var id = RegisterEnemy(enemyData, spawnIndex, layout, rng);
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

        private List<EnemyDataSO> BuildSpawnPlan(RoomSO room, RoomLayout layout, System.Random rng)
        {
            // SpawnPointConfig path: per-spawn-point enemy sets on the prefab.
            if (layout != null && layout.EnemySpawnPoints != null && layout.EnemySpawnPoints.Count > 0)
            {
                var configs = new List<SpawnPointConfig>();
                foreach (var sp in layout.EnemySpawnPoints)
                {
                    if (sp == null) continue;
                    var config = sp.GetComponent<SpawnPointConfig>();
                    if (config != null && config.SetCount > 0)
                        configs.Add(config);
                }

                if (configs.Count > 0)
                {
                    int minSets = int.MaxValue;
                    foreach (var c in configs)
                        if (c.SetCount < minSets) minSets = c.SetCount;

                    int setIndex = rng.Next(0, minSets);

                    var plan = new List<EnemyDataSO>();
                    foreach (var sp in layout.EnemySpawnPoints)
                    {
                        if (sp == null) continue;
                        var config = sp.GetComponent<SpawnPointConfig>();
                        var enemy = config != null ? config.GetEnemyForSet(setIndex) : null;

                        if (enemy != null)
                        {
                            plan.Add(enemy);
                        }
                        else if (room.EnemyPool != null)
                        {
                            var currentRolled = room.EnemyPool.RollForSpawns(1, rng);
                            if (currentRolled.Count > 0) plan.Add(currentRolled[0]);
                        }
                    }
                    return plan;
                }
            }

            // Legacy path: PossibleSetups then EnemyPool.
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

        private Guid RegisterEnemy(EnemyDataSO enemyData, int spawnIndex, RoomLayout layout, System.Random rng)
        {
            var coord = ResolveSpawnCoord(layout, spawnIndex);
            return RegisterEnemyAtCoord(enemyData, coord, rng, state: null);
        }

        /// <summary>
        /// Path "core" de registro: dado un <paramref name="coord"/> ya resuelto, registra
        /// la entidad en todos los servicios (registry, attributes, AI, grid, visuals,
        /// gold drops). Si <paramref name="state"/> no es null, restaura el HP del state.
        /// </summary>
        private Guid RegisterEnemyAtCoord(
            EnemyDataSO enemyData, GridCoord coord, System.Random rng, EnemySpawnState state)
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

            if (_grid != null) _grid.Register(id, coord);
            if (_visuals != null) _visuals.SpawnEnemy(id, enemyData, coord);

            int hp = state != null ? Math.Max(0, state.CurrentHP) : enemyData.BaseHP;
            if (state != null)
            {
                var health = _attributes.GetAttribute<Rollgeon.Attributes.Stats.Health>(id);
                if (health != null) health.Value = hp;
            }

            if (_visuals != null && _visuals.TryGetPawn(id, out var pawn) && pawn.HealthBar != null)
                pawn.HealthBar.Initialize(id, hp, enemyData.BaseHP);

            if (_goldDrops != null)
            {
                int drop = RollGoldDrop(enemyData, rng);
                if (drop > 0) _goldDrops.RegisterDrop(id, drop);
            }

            ApplyComboImmunities(enemyData);

            return id;
        }

        /// <summary>
        /// Scanea los <c>Behaviors</c> del enemigo en busca de
        /// <see cref="BossComboImmunityBehavior"/> y aplica el bloqueo de combo
        /// inmediatamente. Sin un dispatcher de behaviors enemigos en runtime, esta
        /// es la forma de garantizar que el boss bloquee el combo configurado
        /// desde el spawn (no requiere esperar a su primer turno).
        /// </summary>
        private static void ApplyComboImmunities(EnemyDataSO enemyData)
        {
            if (enemyData?.Behaviors == null) return;
            UnityEngine.Debug.Log($"[ApplyComboImmunities] enemy='{enemyData.name}' behaviors count={enemyData.Behaviors.Count}");
            foreach (var b in enemyData.Behaviors)
            {
                UnityEngine.Debug.Log($"[ApplyComboImmunities]   behavior type={b?.GetType().Name ?? "null"}");
                if (b is BossComboImmunityBehavior immunity)
                {
                    UnityEngine.Debug.Log($"[ApplyComboImmunities]     ImmuneCombo={immunity.ImmuneCombo?.name ?? "null"} ImmuneCombo.ComboId='{immunity.ImmuneCombo?.ComboId ?? "null"}'");
                    immunity.Execute(null);
                }
            }
        }

        private Guid RegisterEnemyFromState(
            EnemyDataSO enemyData, EnemySpawnState state, RoomLayout layout, System.Random rng)
        {
            var coord = ResolveSpawnCoord(layout, state.SpawnPointIndex);
            return RegisterEnemyAtCoord(enemyData, coord, rng, state);
        }

        /// <summary>
        /// Tiles "no spawneables" derivados del layout: anchors de las 4 puertas.
        /// El player ocupa su propio tile y se filtra automáticamente via
        /// <see cref="IGridManager.IsOccupied"/> en el random pick.
        /// </summary>
        private HashSet<GridCoord> CollectDoorCoords(RoomLayout layout)
        {
            var set = new HashSet<GridCoord>();
            if (layout == null || _grid == null) return set;
            if (layout.DoorSlots == null) return set;
            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.Anchor == null) continue;
                set.Add(_grid.WorldToGrid(slot.Anchor.position));
            }
            return set;
        }

        /// <summary>
        /// Elige un tile aleatorio walkable, no en <paramref name="forbidden"/> y no
        /// ocupado por otra entidad. Devuelve <c>null</c> si no hay candidatos válidos
        /// (grid ausente, NavGraph vacío, o todos los tiles excluidos) — el caller debe
        /// caer al path determinístico.
        /// </summary>
        private GridCoord? TryPickRandomSpawnCoord(HashSet<GridCoord> forbidden, System.Random rng)
        {
            if (_grid == null || _grid.Graph == null) return null;

            var candidates = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords())
            {
                if (forbidden.Contains(coord)) continue;
                if (_grid.IsOccupied(coord)) continue;
                if (!_grid.IsWalkable(coord)) continue;
                candidates.Add(coord);
            }

            if (candidates.Count == 0) return null;
            int pick = rng != null ? rng.Next(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
            return candidates[pick];
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

        private static int RollGoldDrop(EnemyDataSO data, System.Random rng)
        {
            if (data == null) return 0;
            int min = data.MinGoldDrop;
            int max = data.MaxGoldDrop;
            if (max <= min) return Math.Max(0, min);
            return rng != null
                ? rng.Next(min, max + 1)
                : UnityEngine.Random.Range(min, max + 1);
        }
    }
}
