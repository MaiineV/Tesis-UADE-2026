using System;
using System.Collections.Generic;
using Rollgeon.Meta;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Pure planner: dada una <see cref="FloorLayoutSO"/> y un seed devuelve
    /// las cells del piso + el template asignado a cada una. Sin side effects
    /// (no instancia prefabs, no toca services). Lo usan tanto el
    /// <see cref="DungeonManager"/> en runtime como el editor para preview.
    /// </summary>
    public static class FloorTopologyPlanner
    {
        public const int MinRoomCount = 3;

        private static readonly Vector2Int[] CardinalSteps =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
        };

        public sealed class Plan
        {
            public int Seed { get; set; }
            public int TargetCount { get; set; }
            public IReadOnlyList<Vector2Int> Cells { get; set; }
            public IReadOnlyDictionary<Vector2Int, RoomSO> Assignments { get; set; }
            public IReadOnlyDictionary<Vector2Int, RoomType> Types { get; set; }
            public IReadOnlyDictionary<RoomType, int> ResolvedCounts { get; set; }
            public IReadOnlyList<string> Warnings { get; set; }
        }

        public static Plan Generate(FloorLayoutSO layout, int seed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            var rng = new System.Random(seed);
            var warnings = new List<string>();

            var resolved = ResolveSlotCounts(layout, rng);
            int targetCount = 0;
            foreach (var rc in resolved.Values) targetCount += rc;
            targetCount = Math.Max(targetCount, MinRoomCount);

            var cells = GenerateTopology(targetCount, rng);
            var assignments = AssignTemplates(cells, layout, resolved, rng, warnings);

            // Map paralelo de tipo por cell, usando el template asignado o
            // un fallback al tipo del slot esperado cuando el template es null.
            var types = new Dictionary<Vector2Int, RoomType>(cells.Count);
            foreach (var cell in cells)
            {
                if (assignments.TryGetValue(cell, out var room) && room != null)
                    types[cell] = room.Type;
                else
                    types[cell] = RoomType.Combat;
            }

            return new Plan
            {
                Seed = seed,
                TargetCount = targetCount,
                Cells = cells,
                Assignments = assignments,
                Types = types,
                ResolvedCounts = resolved,
                Warnings = warnings,
            };
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        internal static Dictionary<RoomType, int> ResolveSlotCounts(
            FloorLayoutSO layout, System.Random rng)
        {
            var result = new Dictionary<RoomType, int>();
            if (layout.Slots == null) return result;

            foreach (var slot in layout.Slots)
            {
                if (slot == null) continue;
                int n = slot.Count != null ? slot.Count.Resolve(rng) : 0;
                if (n <= 0) continue;
                result.TryGetValue(slot.Type, out var prev);
                result[slot.Type] = prev + n;
            }
            return result;
        }

        internal static List<Vector2Int> GenerateTopology(int targetCount, System.Random rng)
        {
            var cells = new List<Vector2Int> { Vector2Int.zero };
            var frontier = new HashSet<Vector2Int> { Vector2Int.zero };
            var used = new HashSet<Vector2Int> { Vector2Int.zero };

            while (cells.Count < targetCount && frontier.Count > 0)
            {
                Vector2Int seed;
                {
                    int pick = rng.Next(frontier.Count);
                    int idx = 0;
                    seed = Vector2Int.zero;
                    foreach (var f in frontier)
                    {
                        if (idx++ == pick) { seed = f; break; }
                    }
                }

                var candidates = new List<Vector2Int>(4);
                foreach (var step in CardinalSteps)
                {
                    var c = seed + step;
                    if (!used.Contains(c)) candidates.Add(c);
                }
                if (candidates.Count == 0) { frontier.Remove(seed); continue; }

                var next = candidates[rng.Next(candidates.Count)];
                cells.Add(next);
                used.Add(next);
                frontier.Add(next);
            }
            return cells;
        }

        internal static Dictionary<Vector2Int, RoomSO> AssignTemplates(
            List<Vector2Int> cells, FloorLayoutSO layout,
            Dictionary<RoomType, int> resolved, System.Random rng,
            List<string> warnings)
        {
            var assignments = new Dictionary<Vector2Int, RoomSO>(cells.Count);
            if (cells.Count == 0) return assignments;

            var poolsByType = BuildPoolsByType(layout);
            var startCell = Vector2Int.zero;
            var remaining = new HashSet<Vector2Int>(cells);

            // Start
            if (resolved.TryGetValue(RoomType.Start, out var startCount) && startCount > 0
                && remaining.Contains(startCell))
            {
                assignments[startCell] = PickRandom(poolsByType.GetValueOrDefault(RoomType.Start), rng);
                remaining.Remove(startCell);
            }

            // Boss(es)
            if (resolved.TryGetValue(RoomType.Boss, out var bossCount) && bossCount > 0)
            {
                var farthest = new List<Vector2Int>(remaining);
                farthest.Sort((a, b) =>
                    ManhattanFromStart(b).CompareTo(ManhattanFromStart(a)));
                int take = Math.Min(bossCount, farthest.Count);
                for (int i = 0; i < take; i++)
                {
                    assignments[farthest[i]] = PickRandom(poolsByType.GetValueOrDefault(RoomType.Boss), rng);
                    remaining.Remove(farthest[i]);
                }
                if (take < bossCount)
                    warnings.Add($"Boss: pedía {bossCount}, cupieron {take}.");
            }

            // Special types. El orden es explícito (no iterar `resolved`) para
            // que la colocación sea determinística contra un mismo seed. Todo
            // tipo resuelto que NO tenga colocación dedicada (Start/Boss arriba,
            // Combat abajo) debe estar acá; si falta, su count infla el target
            // pero la cell cae al fallback de Combat y la sala nunca aparece.
            var specialOrder = new[] { RoomType.Shop, RoomType.Potion, RoomType.Enchantment };
            foreach (var type in specialOrder)
            {
                if (!resolved.TryGetValue(type, out var count) || count <= 0) continue;
                var pool = poolsByType.GetValueOrDefault(type);
                int placed = 0;
                while (placed < count && remaining.Count > 0)
                {
                    var cell = PickRandomFromSet(remaining, rng);
                    assignments[cell] = PickRandom(pool, rng);
                    remaining.Remove(cell);
                    placed++;
                }
                if (placed < count)
                    warnings.Add($"{type}: pedía {count}, cupieron {placed}.");
            }

            // Combat
            int combatCount = resolved.TryGetValue(RoomType.Combat, out var rc) ? rc : 0;
            var combatPool = poolsByType.GetValueOrDefault(RoomType.Combat);
            int combatPlaced = 0;
            foreach (var cell in new List<Vector2Int>(remaining))
            {
                if (combatPlaced >= combatCount) break;
                assignments[cell] = PickRandom(combatPool, rng);
                remaining.Remove(cell);
                combatPlaced++;
            }
            if (combatPlaced < combatCount)
                warnings.Add($"Combat: pedía {combatCount}, cupieron {combatPlaced}.");

            // Overflow cells → combat fallback
            foreach (var cell in remaining)
                assignments[cell] = PickRandom(combatPool, rng);

            return assignments;
        }

        internal static Dictionary<RoomType, List<RoomSO>> BuildPoolsByType(FloorLayoutSO layout)
        {
            var pools = new Dictionary<RoomType, List<RoomSO>>();
            if (layout.Slots == null) return pools;
            foreach (var slot in layout.Slots)
            {
                if (slot == null || slot.Pool == null) continue;
                if (!pools.TryGetValue(slot.Type, out var list))
                {
                    list = new List<RoomSO>();
                    pools[slot.Type] = list;
                }
                foreach (var room in slot.Pool)
                {
                    if (room == null || list.Contains(room)) continue;
                    // Meta-progresión (#164): salas gateadas quedan fuera de la
                    // generación hasta desbloquearse. Sin servicio registrado
                    // (tests, preview de editor) el gate degrada a disponible.
                    if (!MetaUnlockGate.IsAvailable(UnlockableCategory.SpecialRoom, room.RoomId)) continue;
                    list.Add(room);
                }
            }
            return pools;
        }

        internal static int ManhattanFromStart(Vector2Int c) =>
            Math.Abs(c.x) + Math.Abs(c.y);

        internal static Vector2Int PickRandomFromSet(HashSet<Vector2Int> set, System.Random rng)
        {
            int pick = rng.Next(set.Count);
            int i = 0;
            foreach (var c in set)
            {
                if (i++ == pick) return c;
            }
            return Vector2Int.zero;
        }

        internal static T PickRandom<T>(IList<T> list, System.Random rng) where T : class
        {
            if (list == null || list.Count == 0) return null;
            return list[rng.Next(list.Count)];
        }
    }
}
