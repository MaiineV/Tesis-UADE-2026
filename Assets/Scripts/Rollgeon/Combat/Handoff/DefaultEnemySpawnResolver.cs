using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Weakness;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Economy;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
using Rollgeon.Entities.Portraits;
using Rollgeon.Entities.Traits;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Run;
using UnityEngine;

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

        /// <summary>Un enemigo planificado para spawn + su tier resuelto por piso (#158).</summary>
        private readonly struct PlannedSpawn
        {
            public readonly EnemyDataSO Enemy;
            public readonly int Tier;
            public PlannedSpawn(EnemyDataSO enemy, int tier) { Enemy = enemy; Tier = tier; }
        }

        private readonly InMemoryEntityRegistry _registry;
        private readonly AttributesManager _attributes;
        private readonly IEnemyAIRegistry _aiRegistry;
        private readonly IGridManager _grid;
        private readonly IEntityVisualService _visuals;
        private readonly EnemyGoldDropService _goldDrops;
        private readonly IEntityPortraitResolver _portraits;
        private readonly IRunContextService _runContext;
        private readonly IFloorProgressionService _floorProgression;

        /// <summary>
        /// One-shot: cuando es <c>true</c>, el próximo re-spawn desde estado guardado usa
        /// la tile y el GUID persistidos (<see cref="EnemySpawnState.LastCell"/>/<c>Guid</c>)
        /// en vez de reposicionar random. Lo prende <c>RunController</c> tras
        /// <c>DungeonManager.ResumeFromSave</c> y se consume en el primer <see cref="Resolve"/>.
        /// El re-entry normal (dentro de la sesión) queda con <c>false</c> ⇒ posición random
        /// (diseño GD). (#0028 Fase 2)
        /// </summary>
        public bool ResumeFromSaveNextSpawn { get; set; }

        public DefaultEnemySpawnResolver(
            InMemoryEntityRegistry registry,
            AttributesManager attributes,
            IEnemyAIRegistry aiRegistry = null,
            IGridManager grid = null,
            IEntityVisualService visuals = null,
            EnemyGoldDropService goldDrops = null,
            IEntityPortraitResolver portraits = null,
            IRunContextService runContext = null,
            IFloorProgressionService floorProgression = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _aiRegistry = aiRegistry;
            _grid = grid;
            _visuals = visuals;
            _goldDrops = goldDrops;
            _portraits = portraits;
            _runContext = runContext;
            _floorProgression = floorProgression;
        }

        /// <summary>
        /// Piso actual (1-based) leído en cada spawn — el resolver vive toda la run y
        /// el piso avanza. Sin servicio (tests / tutorial sin progresión) ⇒ piso 1.
        /// </summary>
        private int CurrentFloorNumber => _runContext != null ? _runContext.FloorIndex + 1 : 1;

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
                // Resume desde save (#0028): un solo spawn respeta la tile y el GUID
                // guardados; el re-entry normal reposiciona random para que la sala no se
                // sienta estática (diseño GD). Flag one-shot.
                bool resume = ResumeFromSaveNextSpawn;
                ResumeFromSaveNextSpawn = false;

                // En re-entry normal, los enemigos vivos reaparecen en posiciones
                // aleatorias. Excluimos tiles de puerta (no caer encima) y los ya
                // ocupados (player + otros enemigos del batch). Si no hay grid (tests
                // sin layout) o no hay candidatos válidos, caemos al spawn point legacy.
                var forbidden = CollectDoorCoords(layout);
                foreach (var state in existingStates)
                {
                    if (state.IsDead) continue;
                    var data = LookupEnemyData(instance, state.EnemyDataSOId);
                    if (data == null) continue;

                    Guid? presetId = null;
                    if (resume && !string.IsNullOrEmpty(state.Guid)
                        && Guid.TryParse(state.Guid, out var savedGuid))
                    {
                        presetId = savedGuid;
                    }

                    Guid id;
                    if (resume && state.HasLastCell)
                    {
                        // Posición exacta guardada.
                        id = RegisterEnemyAtCoord(data, state.LastCell, rng, state, state.Tier, presetId);
                    }
                    else
                    {
                        var randomCoord = TryPickRandomSpawnCoord(forbidden, rng, data.EffectiveFootprint);
                        id = randomCoord.HasValue
                            ? RegisterEnemyAtCoord(data, randomCoord.Value, rng, state, state.Tier, presetId)
                            : RegisterEnemyFromState(data, state, layout, rng, presetId);
                    }

                    if (id != Guid.Empty)
                    {
                        result.Add((id, data));
                        instance.SpawnedEnemies.Add(id);
                    }
                }
                return result;
            }

            // 2. Primer spawn de la sala.
            var plan = BuildSpawnPlan(room, layout, rng, instance.Boss);
            int spawnIndex = 0;
            foreach (var planned in plan)
            {
                var enemyData = planned.Enemy;
                if (enemyData == null) continue;

                var id = RegisterEnemy(enemyData, planned.Tier, spawnIndex, instance, layout, rng);
                if (id != Guid.Empty)
                {
                    result.Add((id, enemyData));
                    instance.SpawnedEnemies.Add(id);

                    instance.ObjectStates.Set(EnemyStateKey(spawnIndex), new EnemySpawnState
                    {
                        SpawnPointId = EnemyStateKey(spawnIndex),
                        EnemyDataSOId = enemyData.EntityId,
                        CurrentHP = enemyData.ResolveMaxHP(planned.Tier),
                        IsDead = false,
                        SpawnPointIndex = spawnIndex,
                        Tier = planned.Tier
                    });
                }
                spawnIndex++;
            }

            return result;
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private List<PlannedSpawn> BuildSpawnPlan(
            RoomSO room, RoomLayout layout, System.Random rng, EnemyDataSO rolledBoss)
        {
            int floor = CurrentFloorNumber;

            // Salas autoradas (tutorial): el setup del SO le gana a los
            // SpawnPointConfig del prefab compartido.
            if (room.ForcePossibleSetups)
            {
                var forced = BuildPlanFromSetups(room, rng, floor);
                if (forced != null) return forced;
            }

            // Boss pool del piso: le gana a los SpawnPointConfig del prefab. Los 3 prefabs
            // de boss room traen su boss clavado en el spawn point, así que la precedencia
            // se resuelve acá por código — no vaciando data que el resto del wiring usa.
            if (room.Type == RoomType.Boss)
            {
                var boss = ResolveBossForFloor(rng, rolledBoss);
                if (boss != null)
                {
                    return new List<PlannedSpawn>
                    {
                        new PlannedSpawn(boss, boss.ResolveTierForFloor(floor))
                    };
                }
            }

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

                    var plan = new List<PlannedSpawn>();
                    foreach (var sp in layout.EnemySpawnPoints)
                    {
                        if (sp == null) continue;
                        var config = sp.GetComponent<SpawnPointConfig>();
                        var enemy = config != null ? config.GetEnemyForSet(setIndex) : null;

                        if (enemy != null)
                        {
                            plan.Add(new PlannedSpawn(enemy, enemy.ResolveTierForFloor(floor)));
                        }
                        else if (room.EnemyPool != null)
                        {
                            var idxs = room.EnemyPool.RollForSpawnIndices(1, rng);
                            if (idxs.Count > 0 && idxs[0] >= 0)
                            {
                                var e = room.EnemyPool.Entries[idxs[0]].Item;
                                int tier = e != null ? e.ResolveTierForFloor(floor) : 1;
                                plan.Add(new PlannedSpawn(e, tier));
                            }
                        }
                    }
                    return plan;
                }
            }

            // Legacy path: PossibleSetups then EnemyPool.
            var fromSetups = BuildPlanFromSetups(room, rng, floor);
            if (fromSetups != null) return fromSetups;

            int defaultCount = room.Type == RoomType.Boss
                ? BossDefaultSpawnCount
                : CombatDefaultSpawnCount;

            if (room.EnemyPool == null) return new List<PlannedSpawn>();

            var rolledIndices = room.EnemyPool.RollForSpawnIndices(defaultCount, rng);
            var list = new List<PlannedSpawn>(rolledIndices.Count);
            foreach (var idx in rolledIndices)
            {
                var e = idx >= 0 ? room.EnemyPool.Entries[idx].Item : null;
                int tier = e != null ? e.ResolveTierForFloor(floor) : 1;
                list.Add(new PlannedSpawn(e, tier));
            }
            return list;
        }

        /// <summary>
        /// Plan desde <see cref="RoomSO.PossibleSetups"/> (uno al azar). <c>null</c>
        /// si no hay setups usables — el caller sigue con su fallback.
        /// </summary>
        private static List<PlannedSpawn> BuildPlanFromSetups(RoomSO room, System.Random rng, int floor)
        {
            if (room.PossibleSetups == null || room.PossibleSetups.Count == 0) return null;

            var setup = room.PossibleSetups[rng.Next(room.PossibleSetups.Count)];
            if (setup?.Slots == null || setup.Slots.Count == 0) return null;

            var plan = new List<PlannedSpawn>(setup.Slots.Count);
            foreach (var slot in setup.Slots)
            {
                int tier = slot.Enemy != null ? slot.Enemy.ResolveTierForFloor(floor) : 1;
                plan.Add(new PlannedSpawn(slot.Enemy, tier));
            }
            return plan;
        }

        /// <summary>
        /// Boss de la sala, por precedencia: el override one-shot de la dev console, después el
        /// que roleó la generación del piso, y por último el <see cref="BossPoolSO"/> del piso
        /// actual. <c>null</c> en todos los eslabones (sin override, sin boss rolado, sin
        /// progresión, piso sin pool, pool sin entries) ⇒ el caller sigue con el path de spawn
        /// de siempre, sin ruido en consola.
        /// </summary>
        /// <param name="rolledBoss">
        /// El de <c>RoomInstance.Boss</c>: lo decidió la generación junto con la sala, así que
        /// re-rolear acá daría un boss que no se corresponde con la sala instanciada.
        /// </param>
        private EnemyDataSO ResolveBossForFloor(System.Random rng, EnemyDataSO rolledBoss)
        {
            if (ServiceLocator.TryGetService<IBossSelectionOverride>(out var bossOverride)
                && bossOverride != null
                && bossOverride.TryConsume(out var forcedBoss)
                && forcedBoss != null)
            {
                return forcedBoss;
            }

            if (rolledBoss != null) return rolledBoss;

            var progression = _floorProgression;
            if (progression == null)
                ServiceLocator.TryGetService<IFloorProgressionService>(out progression);
            if (progression == null) return null;

            var layout = progression.CurrentLayout;
            var pool = layout != null ? layout.BossPool : null;
            if (pool == null) return null;

            return pool.Roll(rng);
        }

        private Guid RegisterEnemy(
            EnemyDataSO enemyData, int tier, int spawnIndex,
            RoomInstance instance, RoomLayout layout, System.Random rng)
        {
            var coord = ResolveSpawnCoord(layout, spawnIndex);

            if (spawnIndex == 0 && instance?.Template != null && instance.Template.Type == RoomType.Boss)
                coord = ResolveBossSpawnCoord(layout, coord);

            // Seam opcional (Tutorial Mode): redirigir la casilla del primer spawn.
            if (ServiceLocator.TryGetService<IEnemySpawnCoordOverride>(out var coordOverride)
                && coordOverride != null
                && coordOverride.TryOverrideSpawnCoord(instance, spawnIndex, coord, out var overridden))
            {
                coord = overridden;
            }

            return RegisterEnemyAtCoord(enemyData, coord, rng, state: null, tier: tier);
        }

        /// <summary>
        /// Path "core" de registro: dado un <paramref name="coord"/> ya resuelto, registra
        /// la entidad en todos los servicios (registry, attributes, AI, grid, visuals,
        /// gold drops). Si <paramref name="state"/> no es null, restaura el HP del state.
        /// </summary>
        private Guid RegisterEnemyAtCoord(
            EnemyDataSO enemyData, GridCoord coord, System.Random rng, EnemySpawnState state, int tier,
            Guid? presetId = null)
        {
            // presetId != null en resume (#0028): preserva el GUID guardado para que la
            // cola de turnos / modifiers restaurados referencien la misma entidad.
            var id = presetId ?? Guid.NewGuid();
            int maxHp = enemyData.ResolveMaxHP(tier);
            var attrs = enemyData.CreateRuntimeStats(tier);
            _registry.Register(id, attrs);
            _attributes.Register(id, attrs);
            if (_portraits != null) _portraits.Register(id, enemyData.Portrait);

            if (_aiRegistry != null)
            {
                var aiRoot = enemyData.CreateRuntimeAIRoot();
                _aiRegistry.Register(id, aiRoot, maxHp);
            }

            if (_grid != null)
            {
                var footprint = enemyData.EffectiveFootprint;
                coord = ResolveFootprintAnchor(coord, footprint, enemyData);
                if (!_grid.TryRegister(id, coord, footprint))
                {
                    // Ya se intentó correr el ancla: el rectángulo no cabe. Se registra 1×1 para que
                    // el combate arranque igual; el pawn se dibuja grande sobre una celda.
                    Debug.LogError($"[DefaultEnemySpawnResolver] '{enemyData.name}' ({footprint.x}×{footprint.y}) no cabe cerca de {coord}: se registra 1×1.");
                    _grid.Register(id, coord);
                }
            }
            if (_visuals != null) _visuals.SpawnEnemy(id, enemyData, coord);

            int hp = state != null ? Math.Max(0, state.CurrentHP) : maxHp;
            if (state != null)
            {
                var health = _attributes.GetAttribute<Rollgeon.Attributes.Stats.Health>(id);
                if (health != null) health.Value = hp;
            }

            if (_visuals != null && _visuals.TryGetPawn(id, out var pawn) && pawn.HealthBar != null)
                pawn.HealthBar.Initialize(id, hp, maxHp);

            if (_goldDrops != null)
            {
                int drop = RollGoldDrop(enemyData, rng);
                if (drop > 0) _goldDrops.RegisterDrop(id, drop);
            }

            ApplyComboImmunities(enemyData);
            RegisterWeakness(id, enemyData);
            RegisterTraits(id, enemyData);

            return id;
        }

        /// <summary>
        /// Registra los <see cref="UnitTraits"/> del enemigo (Flying/Boss/personalidad IA)
        /// para Casillas Especiales y pathing. Tolerante a ausencia del servicio: sin él,
        /// los consumidores caen al default terrestre/Normal.
        /// </summary>
        private static void RegisterTraits(Guid id, EnemyDataSO enemyData)
        {
            if (enemyData == null) return;
            if (ServiceLocator.TryGetService<IUnitTraitService>(out var traits) && traits != null)
                traits.Register(id, enemyData.CreateTraits());
        }

        /// <summary>
        /// Registra la debilidad del enemigo (combo → multiplicador) para que el
        /// <see cref="Rollgeon.Combat.Pipelines.DamagePipeline"/> la aplique al pegarle con ese
        /// combo. Sin <c>WeaknessComboId</c> ("None") no se registra ⇒ el checker resuelve ×1.0.
        /// </summary>
        private static void RegisterWeakness(Guid id, EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.WeaknessComboId)) return;
            if (ServiceLocator.TryGetService<IWeaknessRegistry>(out var registry) && registry != null)
                registry.SetWeakness(id, enemyData.WeaknessComboId, enemyData.WeaknessMultiplierOverride);
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
            EnemyDataSO enemyData, EnemySpawnState state, RoomLayout layout, System.Random rng,
            Guid? presetId = null)
        {
            var coord = ResolveSpawnCoord(layout, state.SpawnPointIndex);
            return RegisterEnemyAtCoord(enemyData, coord, rng, state, state.Tier, presetId);
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
            => TryPickRandomSpawnCoord(forbidden, rng, GridFootprint.Unit);

        private GridCoord? TryPickRandomSpawnCoord(HashSet<GridCoord> forbidden, System.Random rng, Vector2Int footprint)
        {
            if (_grid == null || _grid.Graph == null) return null;

            var candidates = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords())
            {
                if (forbidden.Contains(coord)) continue;
                if (_grid.IsOccupied(coord)) continue;
                if (!_grid.IsWalkable(coord)) continue;
                if (!GridFootprint.IsUnit(footprint) && !FootprintFits(coord, footprint, forbidden)) continue;
                // BUG-069: un nodo walkable puede tener grado 0 (isla del NavGraph) —
                // un enemigo spawneado ahí queda inalcanzable y sin poder moverse.
                if (!_grid.Graph.IsEmpty && !HasAnyNeighbor(coord)) continue;
                candidates.Add(coord);
            }

            if (candidates.Count == 0) return null;
            int pick = rng != null ? rng.Next(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
            return candidates[pick];
        }

        /// <summary>El rectángulo entero cabe (walkable + libre) y ninguna de sus celdas está prohibida.</summary>
        private bool FootprintFits(GridCoord anchor, Vector2Int footprint, HashSet<GridCoord> forbidden)
        {
            if (!_grid.CanPlace(anchor, footprint)) return false;
            if (forbidden == null) return true;
            foreach (var c in GridFootprint.Cells(anchor, footprint))
                if (forbidden.Contains(c)) return false;
            return true;
        }

        /// <summary>Radio (Chebyshev) en el que se busca un ancla alternativa para un footprint que no cabe.</summary>
        public const int FootprintShiftRadius = 3;

        /// <summary>
        /// Un 1×1 vuelve tal cual. Para un rectángulo que no cabe en el ancla pedida, barre anclas
        /// cercanas en orden determinista (anillos Chebyshev 1..<see cref="FootprintShiftRadius"/>,
        /// dentro del anillo por Manhattan, X, Y) y devuelve la primera que cabe; si ninguna cabe
        /// devuelve la original (el caller decide el fallback).
        /// </summary>
        private GridCoord ResolveFootprintAnchor(GridCoord desired, Vector2Int footprint, EnemyDataSO data)
        {
            if (_grid == null || GridFootprint.IsUnit(footprint)) return desired;
            if (_grid.CanPlace(desired, footprint)) return desired;

            var ring = new List<GridCoord>();
            for (int r = 1; r <= FootprintShiftRadius; r++)
            {
                ring.Clear();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r)
                            ring.Add(new GridCoord(desired.X + dx, desired.Y + dy));
                ring.Sort((a, b) =>
                {
                    int c = desired.Manhattan(a).CompareTo(desired.Manhattan(b));
                    if (c != 0) return c;
                    c = a.X.CompareTo(b.X);
                    return c != 0 ? c : a.Y.CompareTo(b.Y);
                });
                foreach (var anchor in ring)
                {
                    if (!_grid.CanPlace(anchor, footprint)) continue;
                    Debug.LogWarning($"[DefaultEnemySpawnResolver] '{data.name}' ({footprint.x}×{footprint.y}) no cabe en {desired}: corrido a {anchor}.");
                    return anchor;
                }
            }
            return desired;
        }

        private bool HasAnyNeighbor(GridCoord coord)
        {
            foreach (var _ in _grid.Graph.GetNeighbors(coord)) return true;
            return false;
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

        /// <summary>
        /// Resuelve el <see cref="EnemyDataSO"/> de un <see cref="EnemySpawnState"/>
        /// guardado, por su <c>EntityId</c>. Bosses (BUG-078): el spawn inicial de una
        /// boss room NO pasa por <c>PossibleSetups</c>/<c>EnemyPool</c> (precedencia de
        /// código en <see cref="BuildSpawnPlan"/>, ver <see cref="ResolveBossForFloor"/>),
        /// así que el resume/re-entry normal nunca lo encontraba ahí — <c>continue</c>
        /// silencioso, combate arrancaba sin el boss, softlock. Dos fallbacks antes de
        /// rendirse: <see cref="RoomInstance.Boss"/> (el que roleó la generación del
        /// piso — reproducible, mismo seed derivado en load) y el
        /// <see cref="Rollgeon.Dungeon.FloorLayoutSO.BossPool"/> vigente (cubre un boss
        /// forzado por dev console cuyo id tampoco vive en <c>instance.Boss</c>).
        /// </summary>
        private EnemyDataSO LookupEnemyData(RoomInstance instance, string entityId)
        {
            var room = instance.Template;

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

            if (instance.Boss != null && instance.Boss.EntityId == entityId) return instance.Boss;

            var progression = _floorProgression;
            if (progression == null)
                ServiceLocator.TryGetService<IFloorProgressionService>(out progression);
            var bossPool = progression?.CurrentLayout?.BossPool;
            if (bossPool?.Entries != null)
            {
                foreach (var entry in bossPool.Entries)
                {
                    if (entry?.Boss != null && entry.Boss.EntityId == entityId) return entry.Boss;
                }
            }

            UnityEngine.Debug.LogError(
                $"[DefaultEnemySpawnResolver] LookupEnemyData: sin match para EntityId='{entityId}' " +
                $"en room='{room.RoomId}' (PossibleSetups/EnemyPool/instance.Boss/BossPool del piso). " +
                "El enemigo NO va a re-spawnear (BUG-078) — combate puede arrancar sin él.");
            return null;
        }

        /// <summary>Casillas hacia adentro desde la pared opuesta a la puerta de entrada. Con 0 el
        /// jefe queda pegado a la pared y pierde la fila de atrás para huir.</summary>
        public const int BossWallInset = 2;

        /// <summary>
        /// La casilla del jefe: contra la pared opuesta a la puerta por la que se entró, en vez de
        /// la celda autorada del layout.
        /// </summary>
        /// <remarks>
        /// Las salas de jefe traen las cuatro puertas y cuál se abre lo decide la topología del
        /// piso, así que una celda autorada queda lejos de una puerta y encima de la de enfrente.
        /// Cae a <paramref name="authored"/> ante cualquier duda: una posición vieja es mejor que
        /// un jefe sin sala.
        /// </remarks>
        private GridCoord ResolveBossSpawnCoord(RoomLayout layout, GridCoord authored)
        {
            if (_grid == null || layout == null) return authored;

            ServiceLocator.TryGetService<IDungeonService>(out var dungeon);
            if (!RoomEntryResolver.TryResolve(_grid, layout, dungeon?.LastEntryDirection, out var entry))
                return authored;

            return BossEntrySpawnResolver.TryResolveAwayFromEntry(
                _grid, entry, BossWallInset, out var coord)
                ? coord
                : authored;
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
