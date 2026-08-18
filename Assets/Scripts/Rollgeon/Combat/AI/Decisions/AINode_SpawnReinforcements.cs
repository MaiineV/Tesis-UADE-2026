using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.Entities.Portraits;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción de "refuerzos": spawnea <see cref="Count"/> copias de <see cref="EnemyToSpawn"/>
    /// en tiles del borde de la sala (perímetro del bounding box, walkable y libres) y los
    /// suma a la ronda de combate en curso vía <see cref="TurnOrderService.Append"/> — los
    /// nuevos combatientes actúan recién cuando termine la ronda actual, y desde ahí quedan
    /// rotando de forma regular y estable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loop de refuerzos (piso 1). El nodo se tickea CADA turno del boss (va en el Sequence
    /// sin envoltura <c>Once</c>) y se auto-gatea: spawnea la primera oleada al cruzar el
    /// umbral de HP, no vuelve a spawnear mientras quede algún refuerzo vivo, y cuando la
    /// oleada entera muere espera <see cref="RespawnDelayTurns"/> turnos del boss antes de
    /// spawnear la siguiente. Repite mientras el boss viva y siga bajo el umbral.
    /// </para>
    /// <para>
    /// Pensado para ir envuelto en <c>If(PcOwnerHpBelow) → SpawnReinforcements</c> (SIN
    /// <c>Once</c>): el gate de HP decide cuándo el loop está activo; el propio nodo decide
    /// cuándo spawnea. Devuelve <see cref="AIResult.Succeeded"/> en los ticks de espera para
    /// no abortar el Sequence del boss.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpawnReinforcements : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Enemigo a spawnear como refuerzo.")]
        public EnemyDataSO EnemyToSpawn;

        [Tooltip("Cantidad de refuerzos a spawnear en tiles del borde de la sala.")]
        [MinValue(1)]
        public int Count = 2;

        [Tooltip("Turnos del boss a esperar tras aniquilar la oleada antes de spawnear la " +
                 "siguiente. 0 = respawnea de inmediato el próximo turno.")]
        [MinValue(0)]
        // Turns to wait after the wave is wiped before the next wave. 0 = respawn immediately next turn.
        public int RespawnDelayTurns = 2;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto de invocar, sólo en el turno que spawnea de verdad. Vacío = sin animación.")]
        public string SpawnFeedbackId;

        // --- Runtime state (per-combat). NonSerialized: vive solo en la copia runtime del
        // árbol (EnemyDataSO.CreateRuntimeAIRoot → SerializationUtility.CreateCopy), nunca en
        // el asset. Mismo patrón que AINode_Once/_Alternate/_PromulgateRule: una pelea nueva
        // arranca con estos campos en su default (lista null ⇒ re-creada lazy; contadores 0).
        [NonSerialized] private List<Guid> _currentWave;
        [NonSerialized] private int _turnsSinceWaveDied;
        [NonSerialized] private bool _hasSpawnedOnce;

        /// <summary>
        /// Si el último <see cref="Tick"/> spawneó de verdad. El nodo devuelve
        /// <see cref="AIResult.Succeeded"/> también en los turnos de espera, así que sin esto la
        /// animación de invocar correría todos los turnos con la oleada en pie.
        /// </summary>
        [NonSerialized] private bool _spawnedThisTick;

        public override string NodeName =>
            $"Spawn Reinforcements ({Count}x {(EnemyToSpawn != null ? EnemyToSpawn.name : "?")})";

        public override AIResult Tick(AIContext context)
        {
            _spawnedThisTick = false;
            if (context == null || EnemyToSpawn == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (context.Attributes == null) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<InMemoryEntityRegistry>(out var registry) || registry == null)
                return AIResult.Failed;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return AIResult.Failed;

            _currentWave ??= new List<Guid>();

            // ¿Queda algún refuerzo vivo de la oleada actual?
            if (CountAliveInWave(context.Attributes) > 0)
            {
                // Oleada en pie: no spawnear y resetear el contador de respawn (el delay solo
                // corre desde que la oleada queda limpia, no acumula durante su vida).
                _turnsSinceWaveDied = 0;
                return AIResult.Succeeded;
            }

            // Oleada vacía: nunca spawneada, o toda muerta. La primera spawnea ya; las
            // siguientes esperan RespawnDelayTurns turnos del boss desde la aniquilación.
            if (_hasSpawnedOnce && _turnsSinceWaveDied < RespawnDelayTurns)
            {
                _turnsSinceWaveDied++;
                return AIResult.Succeeded;
            }

            var spawned = SpawnWave(context, grid, registry, turnOrder);
            if (spawned == null)
            {
                // Sin tiles de borde válidos: no cambiamos estado, se reintenta el próximo tick.
                Debug.LogWarning("[AINode_SpawnReinforcements] Sin tiles de borde válidos — no se spawnea nada.");
                return AIResult.Failed;
            }

            _currentWave.Clear();
            _currentWave.AddRange(spawned);
            _hasSpawnedOnce = true;
            _turnsSinceWaveDied = 0;
            _spawnedThisTick = true;
            return AIResult.Succeeded;
        }

        /// <remarks>
        /// <para>
        /// Los refuerzos aparecían de la nada: cinco bichos se materializaban en el borde de la
        /// sala mientras el jefe seguía en idle, y no había nada que dijera que los había traído
        /// él. Con el gesto, la reposición se lee como una acción suya y no como un evento de la
        /// sala.
        /// </para>
        /// <para>
        /// Sólo en el tick que spawnea: el nodo también devuelve <c>Succeeded</c> cuando está
        /// esperando (oleada viva o delay corriendo), y animar esos turnos sería el jefe invocando
        /// al aire.
        /// </para>
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result != AIResult.Succeeded || !_spawnedThisTick || string.IsNullOrEmpty(SpawnFeedbackId))
            {
                onResult?.Invoke(result);
                yield break;
            }

            var beat = PlaySpawn(context);
            while (beat.MoveNext()) yield return beat.Current;

            onResult?.Invoke(result);
        }

        /// <remarks>
        /// Request armado a mano porque el nodo no nace de un effect pass y no tiene
        /// <c>EffectContext</c> — el mismo caso que documenta <c>FeedbackRequest.Context</c> como
        /// nullable, y la misma forma que usan los otros nodos de jefe.
        /// </remarks>
        private IEnumerator PlaySpawn(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = SpawnFeedbackId,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el turno le pasa
            // por encima. Mismo degradado que el resto de los nodos de jefe.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <summary>
        /// Cuenta los guids de la oleada actual que siguen vivos. "Vivo" = misma fuente de
        /// verdad que la AI de targeting (<c>TargetSelector_Nearest.IsDead</c>): tiene
        /// <see cref="Health"/> registrada y &gt; 0. Un refuerzo enterrado por
        /// <c>CombatDeathWatcher</c> conserva su Health en &lt;= 0 (no lo desregistra de
        /// <see cref="AttributesManager"/>), así que HP &lt;= 0 o sin registro = muerto.
        /// </summary>
        private int CountAliveInWave(AttributesManager attrs)
        {
            int alive = 0;
            for (int i = 0; i < _currentWave.Count; i++)
            {
                var health = attrs.GetAttribute<Health>(_currentWave[i]);
                if (health != null && health.Value > 0) alive++;
            }
            return alive;
        }

        /// <summary>
        /// Spawnea una oleada de <see cref="Count"/> refuerzos y devuelve sus guids, o
        /// <c>null</c> si la sala no tiene tiles de borde válidos (el caller reintenta).
        /// </summary>
        private List<Guid> SpawnWave(AIContext context, IGridManager grid,
            InMemoryEntityRegistry registry, TurnOrderService turnOrder)
        {
            var rng = context.Rng ?? new System.Random();
            var tiles = PickEdgeSpawnTiles(grid, rng, Count);
            if (tiles.Count == 0) return null;

            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);
            ServiceLocator.TryGetService<IEntityPortraitResolver>(out var portraits);
            var visuals = context.VisualService;

            var spawned = new List<Guid>(tiles.Count);
            const int tier = 1;
            foreach (var coord in tiles)
            {
                var id = Guid.NewGuid();
                var attrs = EnemyToSpawn.CreateRuntimeStats(tier);

                registry.Register(id, attrs);
                context.Attributes.Register(id, attrs);

                // Sin esto el slot del refuerzo en la cola de turnos sale en blanco:
                // IEntityPortraitResolver resuelve por guid contra un dict que puebla quien
                // spawnea (ver DefaultEnemySpawnResolver), y el player es el único con fallback.
                portraits?.Register(id, EnemyToSpawn.Portrait);

                if (aiRegistry != null)
                {
                    var aiRoot = EnemyToSpawn.CreateRuntimeAIRoot();
                    aiRegistry.Register(id, aiRoot, EnemyToSpawn.ResolveMaxHP(tier));
                }

                grid.Register(id, coord);
                visuals?.SpawnEnemy(id, EnemyToSpawn, coord);

                // Reinforcements spawn at full HP; the world-space bar is a caller-initialized
                // widget, so mirror DefaultEnemySpawnResolver — otherwise the bar renders its
                // default (0 HP) and never binds to damage events.
                if (visuals != null && visuals.TryGetPawn(id, out var pawn) && pawn.HealthBar != null)
                {
                    int maxHp = EnemyToSpawn.ResolveMaxHP(tier);
                    pawn.HealthBar.Initialize(id, maxHp, maxHp);
                }

                turnOrder.Append(id);

                // El refuerzo se appendea a la ronda EN CURSO, así que actúa antes de que el
                // jugador vuelva a jugar. Sin este aviso pegaría de una en su turno de aparición
                // (daño gratis, imposible de esquivar). TreeDrivenEnemyAI difiere esa primera
                // activación al recibir el evento — el refuerzo "aparece" sin actuar y recién
                // pega cuando el jugador ya tuvo un turno para reaccionar.
                EventManager.Trigger(EventName.OnReinforcementSpawned, id);

                spawned.Add(id);
            }

            return spawned;
        }

        /// <summary>Distancia Chebyshev mínima entre dos refuerzos — evita que 2 spawns
        /// del mismo lado queden pegados uno al lado del otro.</summary>
        private const int MinSpawnSeparation = 3;

        // Tiles del perímetro del bounding box de la sala (X==min/max o Y==min/max),
        // walkable y libres. Agrupados por lado (W/E/S/N — una esquina puede pertenecer
        // a 2 lados) y repartidos en orden aleatorio de lado para que, con Count>=2, los
        // refuerzos caigan en lados distintos u opuestos en vez de todos apilados en el
        // mismo lado. Sala sin bounds reales (grafo vacío) o sin tiles de borde
        // disponibles ⇒ lista vacía (no crashea).
        private static List<GridCoord> PickEdgeSpawnTiles(IGridManager grid, System.Random rng, int count)
        {
            var result = new List<GridCoord>();
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) return result;

            var allCoords = new List<GridCoord>();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in graph.AllCoords())
            {
                allCoords.Add(c);
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }
            if (allCoords.Count == 0) return result;

            // 0=West (X==minX), 1=East (X==maxX), 2=South (Y==minY), 3=North (Y==maxY).
            var sides = new List<GridCoord>[] { new(), new(), new(), new() };
            foreach (var c in allCoords)
            {
                if (!grid.IsWalkable(c) || grid.IsOccupied(c)) continue;
                if (c.X == minX) sides[0].Add(c);
                if (c.X == maxX) sides[1].Add(c);
                if (c.Y == minY) sides[2].Add(c);
                if (c.Y == maxY) sides[3].Add(c);
            }

            var sideOrder = new List<int> { 0, 1, 2, 3 };
            ShuffleInPlace(sideOrder, rng);

            int guard = sides[0].Count + sides[1].Count + sides[2].Count + sides[3].Count;
            int cursor = 0;
            while (result.Count < count && guard-- > 0)
            {
                var pool = sides[sideOrder[cursor % sideOrder.Count]];
                cursor++;
                if (pool.Count == 0) continue;

                int fallbackIdx = -1;
                int chosenIdx = -1;
                for (int attempt = 0; attempt < pool.Count; attempt++)
                {
                    int idx = rng.Next(pool.Count);
                    fallbackIdx = idx;
                    if (IsFarEnoughFromAll(pool[idx], result, MinSpawnSeparation))
                    {
                        chosenIdx = idx;
                        break;
                    }
                }

                int pick = chosenIdx >= 0 ? chosenIdx : fallbackIdx;
                result.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            return result;
        }

        private static bool IsFarEnoughFromAll(GridCoord c, List<GridCoord> picked, int minSeparation)
        {
            foreach (var p in picked)
                if (Math.Max(Math.Abs(c.X - p.X), Math.Abs(c.Y - p.Y)) < minSeparation)
                    return false;
            return true;
        }

        private static void ShuffleInPlace<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif
    }
}
