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

                turnOrder.Append(id);
            }

            return AIResult.Succeeded;
        }

        // Tiles del perímetro del bounding box de la sala (X==min/max o Y==min/max),
        // walkable y libres — hasta count, sin repetir. Sala sin bounds reales (grafo
        // vacío) o sin tiles de borde disponibles ⇒ lista vacía (no crashea).
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

            var candidates = new List<GridCoord>();
            foreach (var c in allCoords)
            {
                bool onEdge = c.X == minX || c.X == maxX || c.Y == minY || c.Y == maxY;
                if (!onEdge) continue;
                if (!grid.IsWalkable(c) || grid.IsOccupied(c)) continue;
                candidates.Add(c);
            }

            int take = Math.Min(count, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = rng.Next(candidates.Count);
                result.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }

            return result;
        }
    }
}
