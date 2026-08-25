using System;
using System.Collections.Generic;
using Rollgeon.Entities;
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

        /// <summary>
        /// BUG-064: distancia de GRAFO mínima entre el start y la boss room. Con esto la
        /// boss room queda a start→X→Y→boss como mínimo, así que el anillo reservado por
        /// <see cref="ComputeBossRing"/> siempre cruza ≥2 celdas Combat.
        /// </summary>
        public const int MinBossGraphDistance = 3;

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

            /// <summary>
            /// Boss rolado por celda de boss. Se decide acá, en la generación, para que un mismo
            /// seed dé el mismo boss aunque se recargue: el piso se reconstruye del seed y el
            /// resume no persiste topología.
            /// </summary>
            public IReadOnlyDictionary<Vector2Int, EnemyDataSO> BossByCell { get; set; }
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

            // El boss se rolea acá, en la generación, y su sala pisa la que eligió
            // AssignTemplates. Va DESPUÉS de asignar (necesita saber qué celdas son de boss) y
            // ANTES de computar `types` (que se lee de los templates ya definitivos).
            var bossByCell = AssignBosses(layout, seed, assignments, warnings);

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
                BossByCell = bossByCell,
                ResolvedCounts = resolved,
                Warnings = warnings,
            };
        }

        /// <summary>
        /// Rolea el boss de cada celda de tipo Boss y, si esa entry declara sala, la impone
        /// sobre el template que había elegido <see cref="AssignTemplates"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>RNG propio por celda.</b> Se usa <see cref="BossSeed.Derive"/> en vez del rng del
        /// planner justamente para no consumirle draws: un mismo seed sigue produciendo el mismo
        /// piso que antes de que existiera este paso.
        /// </para>
        /// <para>
        /// <b>Entry sin sala ⇒ no se toca nada.</b> El piso que no tenga el vínculo cableado se
        /// queda con la sala del pool, que es el comportamiento de siempre.
        /// </para>
        /// </remarks>
        internal static Dictionary<Vector2Int, EnemyDataSO> AssignBosses(
            FloorLayoutSO layout, int seed,
            Dictionary<Vector2Int, RoomSO> assignments,
            List<string> warnings)
        {
            var result = new Dictionary<Vector2Int, EnemyDataSO>();
            var pool = layout != null ? layout.BossPool : null;
            if (pool == null) return result;

            // Las celdas se juntan antes de tocar `assignments`: imponer la sala del boss lo
            // muta, y mutarlo durante el foreach es undefined en Mono.
            var bossCells = new List<Vector2Int>();
            foreach (var pair in assignments)
            {
                if (pair.Value != null && pair.Value.Type == RoomType.Boss) bossCells.Add(pair.Key);
            }

            foreach (var cell in bossCells)
            {
                var entry = pool.RollEntry(new System.Random(BossSeed.Derive(seed, cell)));
                if (entry?.Boss == null)
                {
                    // Una sala de boss sin boss deja la run sin cierre posible, y el síntoma
                    // (sala vacía al final del piso) no apunta al pool por sí solo.
                    warnings.Add($"Boss: la celda {cell} quedó sin boss — revisá '{pool.name}'.");
                    continue;
                }

                result[cell] = entry.Boss;
                if (entry.Room != null) assignments[cell] = entry.Room;
            }

            return result;
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

            // Boss(es). BUG-064: la boss room tiene que quedar a ≥2 combates del spawn, no
            // solo "lejos" en Manhattan (eso permitía spawn → shop → boss). Se ordena por
            // distancia de GRAFO desc — la métrica que importa para el invariante — y a
            // igualdad por Manhattan desc para conservar el feel de "la más lejana".
            var bossCells = new List<Vector2Int>();
            if (resolved.TryGetValue(RoomType.Boss, out var bossCount) && bossCount > 0)
            {
                var dist = ComputeGraphDistances(cells, startCell);
                var farthest = new List<Vector2Int>(remaining);
                farthest.Sort((a, b) =>
                {
                    int da = dist.TryGetValue(a, out var dda) ? dda : -1;
                    int db = dist.TryGetValue(b, out var ddb) ? ddb : -1;
                    int cmp = db.CompareTo(da);
                    return cmp != 0 ? cmp : ManhattanFromStart(b).CompareTo(ManhattanFromStart(a));
                });
                int take = Math.Min(bossCount, farthest.Count);
                for (int i = 0; i < take; i++)
                {
                    var cell = farthest[i];
                    bossCells.Add(cell);
                    assignments[cell] = PickRandom(poolsByType.GetValueOrDefault(RoomType.Boss), rng);
                    remaining.Remove(cell);

                    // Piso degenerado (muy chico): no hay forma de garantizar 2 combates
                    // antes del boss. Se degrada con warning en vez de tirar excepción.
                    int d = dist.TryGetValue(cell, out var dd) ? dd : -1;
                    if (d < MinBossGraphDistance)
                        warnings.Add($"Boss: la celda {cell} quedó a distancia {d} del start " +
                            $"(< {MinBossGraphDistance}); el piso es muy chico para garantizar 2 combates antes del boss.");
                }
                if (take < bossCount)
                    warnings.Add($"Boss: pedía {bossCount}, cupieron {take}.");
            }

            // Reserva del anillo del boss como Combat (BUG-064). Se saca de `remaining`
            // ANTES de las especiales para que Shop/Potion/Enchantment no lo pisen — ver
            // XML doc de ComputeBossRing para la demostración del invariante.
            var bossRing = ComputeBossRing(cells, bossCells, startCell);
            foreach (var cell in bossRing)
                remaining.Remove(cell);

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

            // El anillo reservado del boss (BUG-064) se coloca primero y SIEMPRE como
            // Combat, exceda o no el presupuesto configurado del layout — es la garantía
            // dura del invariante, no una preferencia de asignación.
            foreach (var cell in bossRing)
            {
                assignments[cell] = PickRandom(combatPool, rng);
                combatPlaced++;
            }
            if (combatPlaced > combatCount)
                warnings.Add($"Combat: el anillo del boss reservó {combatPlaced} celda(s) pero el " +
                    $"layout pedía {combatCount}; se colocan igual para garantizar 2 combates antes del boss.");

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

        /// <summary>
        /// BFS sobre el grafo inducido por <paramref name="cells"/> (4-adyacencia real, no
        /// Manhattan — el piso puede tener ciclos/atajos). Devuelve la distancia mínima en
        /// número de saltos desde <paramref name="start"/> a cada cell alcanzable.
        /// </summary>
        internal static Dictionary<Vector2Int, int> ComputeGraphDistances(
            List<Vector2Int> cells, Vector2Int start)
        {
            var dist = new Dictionary<Vector2Int, int>(cells.Count);
            var cellSet = new HashSet<Vector2Int>(cells);
            if (!cellSet.Contains(start)) return dist;

            var queue = new Queue<Vector2Int>();
            dist[start] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int d = dist[cur];
                foreach (var step in CardinalSteps)
                {
                    var next = cur + step;
                    if (!cellSet.Contains(next) || dist.ContainsKey(next)) continue;
                    dist[next] = d + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        /// <summary>
        /// BUG-064: cells reservadas como Combat alrededor de cada boss cell —
        /// <c>N(boss) ∪ (N(N(boss)) \ {boss})</c>, excluyendo el start y otras boss cells.
        /// </summary>
        /// <remarks>
        /// Con <see cref="MinBossGraphDistance"/> ≥ 3, todo camino start→boss termina en
        /// <c>… → X → Y → boss</c> con <c>Y ∈ N(boss)</c>, <c>X ∈ N(Y)</c> y <c>X ≠ start</c>
        /// (si X fuera start, el camino tendría largo 2, por debajo del mínimo). Reservar
        /// N(boss) y N(N(boss)) como Combat garantiza que ese camino cruce ≥2 Combat sin
        /// importar la ruta elegida — es la sala de boss la que queda "amurallada" por
        /// combates, no una condición probabilística sobre dónde caen las especiales.
        /// </remarks>
        internal static HashSet<Vector2Int> ComputeBossRing(
            List<Vector2Int> cells, List<Vector2Int> bossCells, Vector2Int startCell)
        {
            var ring = new HashSet<Vector2Int>();
            if (bossCells == null || bossCells.Count == 0) return ring;

            var cellSet = new HashSet<Vector2Int>(cells);
            var bossSet = new HashSet<Vector2Int>(bossCells);

            foreach (var boss in bossSet)
            {
                var radius1 = new List<Vector2Int>(4);
                foreach (var step in CardinalSteps)
                {
                    var n1 = boss + step;
                    if (!cellSet.Contains(n1)) continue;
                    radius1.Add(n1);
                    if (n1 != startCell && !bossSet.Contains(n1)) ring.Add(n1);
                }
                foreach (var n1 in radius1)
                {
                    foreach (var step in CardinalSteps)
                    {
                        var n2 = n1 + step;
                        if (!cellSet.Contains(n2) || bossSet.Contains(n2) || n2 == startCell) continue;
                        ring.Add(n2);
                    }
                }
            }
            return ring;
        }

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
