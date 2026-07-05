using System;
using System.Collections.Generic;
using Rollgeon.Dungeon;
using UnityEngine;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Piso autorado a mano para el tutorial: celdas explícitas + sala por celda,
    /// sin topología aleatoria. <see cref="ToPlan"/> lo convierte en un
    /// <see cref="FloorTopologyPlanner.Plan"/> que consume
    /// <see cref="DungeonManager.CreateAndRegisterFromPlan"/> — el wiring de
    /// conexiones lo hace el pipeline downstream por 4-adyacencia, así que la
    /// adyacencia de celdas ES el grafo de puertas.
    /// <para>
    /// SO plano (no Odin) a propósito: lo puede autorar tanto el inspector como
    /// el instalador de assets del tutorial sin datos serializados opacos.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tutorial/Tutorial Floor Plan", fileName = "TutorialFloorPlan")]
    public sealed class TutorialFloorPlanSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Vector2Int Cell;
            public RoomSO Room;

            [Tooltip("La sala nace Locked (gate externo del tutorial) en vez de su estado default.")]
            public bool StartLocked;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;

#if UNITY_EDITOR
        /// <summary>Solo para tooling de editor (instalador de assets del tutorial).</summary>
        public void SetEntries(List<Entry> entries) => _entries = entries;
#endif

        /// <summary>
        /// Convierte el plan autorado al formato del planner. Tira
        /// <see cref="InvalidOperationException"/> si el plan es inválido:
        /// vacío, celdas duplicadas, salas null, sin Start en (0,0)
        /// (requisito de <c>DungeonManager.FindStartInstanceId</c>), o no contiguo.
        /// </summary>
        public FloorTopologyPlanner.Plan ToPlan()
        {
            if (_entries == null || _entries.Count == 0)
                throw new InvalidOperationException($"[{name}] TutorialFloorPlanSO sin entries.");

            var cells = new List<Vector2Int>(_entries.Count);
            var assignments = new Dictionary<Vector2Int, RoomSO>(_entries.Count);
            var types = new Dictionary<Vector2Int, RoomType>(_entries.Count);
            int startCount = 0;

            foreach (var entry in _entries)
            {
                if (entry == null || entry.Room == null)
                    throw new InvalidOperationException($"[{name}] Entry con Room null.");
                if (assignments.ContainsKey(entry.Cell))
                    throw new InvalidOperationException($"[{name}] Celda duplicada {entry.Cell}.");

                cells.Add(entry.Cell);
                assignments[entry.Cell] = entry.Room;
                types[entry.Cell] = entry.Room.Type;

                if (entry.Room.Type == RoomType.Start)
                {
                    startCount++;
                    if (entry.Cell != Vector2Int.zero)
                        throw new InvalidOperationException(
                            $"[{name}] La Start room debe estar en (0,0) — está en {entry.Cell}.");
                }
            }

            if (startCount != 1)
                throw new InvalidOperationException(
                    $"[{name}] Debe haber exactamente 1 Start room (hay {startCount}).");

            ValidateContiguity(cells);

            return new FloorTopologyPlanner.Plan
            {
                Seed = 0,
                TargetCount = cells.Count,
                Cells = cells,
                Assignments = assignments,
                Types = types,
                ResolvedCounts = CountByType(types),
                Warnings = Array.Empty<string>(),
            };
        }

        /// <summary>
        /// BFS por 4-adyacencia desde (0,0) — una celda inalcanzable sería una
        /// sala sin puertas hacia el resto del piso.
        /// </summary>
        private void ValidateContiguity(List<Vector2Int> cells)
        {
            var all = new HashSet<Vector2Int>(cells);
            var visited = new HashSet<Vector2Int> { Vector2Int.zero };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);

            var steps = new[]
            {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
            };

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var step in steps)
                {
                    var n = cell + step;
                    if (all.Contains(n) && visited.Add(n)) queue.Enqueue(n);
                }
            }

            if (visited.Count != all.Count)
                throw new InvalidOperationException(
                    $"[{name}] Plan no contiguo: {all.Count - visited.Count} celda(s) inalcanzable(s) desde (0,0).");
        }

        private static Dictionary<RoomType, int> CountByType(Dictionary<Vector2Int, RoomType> types)
        {
            var counts = new Dictionary<RoomType, int>();
            foreach (var type in types.Values)
            {
                counts.TryGetValue(type, out var prev);
                counts[type] = prev + 1;
            }
            return counts;
        }
    }
}
