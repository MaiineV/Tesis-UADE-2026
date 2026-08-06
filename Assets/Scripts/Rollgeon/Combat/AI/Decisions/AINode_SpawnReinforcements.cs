using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Initiative;
using Rollgeon.Entities;
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
    /// Pensado para usarse envuelto en <c>If(PcOwnerHpBelow) → Once(...)</c>, igual que el
    /// trigger de fase existente — dispara una sola vez al cruzar el umbral de HP.
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

        public override string NodeName =>
            $"Spawn Reinforcements ({Count}x {(EnemyToSpawn != null ? EnemyToSpawn.name : "?")})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || EnemyToSpawn == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (context.Attributes == null) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<InMemoryEntityRegistry>(out var registry) || registry == null)
                return AIResult.Failed;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return AIResult.Failed;

            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);
            var visuals = context.VisualService;

            var rng = context.Rng ?? new System.Random();
            var tiles = PickEdgeSpawnTiles(grid, rng, Count);
            if (tiles.Count == 0)
            {
                Debug.LogWarning("[AINode_SpawnReinforcements] Sin tiles de borde válidos — no se spawnea nada.");
                return AIResult.Failed;
            }

            const int tier = 1;
            foreach (var coord in tiles)
            {
                var id = Guid.NewGuid();
                var attrs = EnemyToSpawn.CreateRuntimeStats(tier);

                registry.Register(id, attrs);
                context.Attributes.Register(id, attrs);

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
            }

            return AIResult.Succeeded;
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
    }
}
