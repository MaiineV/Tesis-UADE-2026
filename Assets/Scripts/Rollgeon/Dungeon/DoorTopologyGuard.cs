using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.Components;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Guard de coherencia entre el grafo lógico de conexiones y las puertas físicas
    /// autoradas en los prefabs (PUL-011). La generación (<see cref="DungeonManager"/>)
    /// cablea <see cref="RoomInstance.Connections"/> por pura adyacencia de grilla, sin
    /// mirar si el prefab asignado tiene una puerta para esa dirección. Cuando no la
    /// tiene, el minimapa muestra la conexión pero no hay paso: sala inalcanzable.
    /// <para>
    /// Esta clase es lógica de grafo pura (sin Unity) para poder testearla: recibe un
    /// predicado <c>hasDoor</c> que el <see cref="DungeonManager"/> resuelve contra los
    /// <c>RoomLayout.DoorSlots</c> del prefab.
    /// </para>
    /// </summary>
    public static class DoorTopologyGuard
    {
        /// <summary>Una conexión intraversable, a podar de ambos lados del grafo.</summary>
        public struct DoorlessEdge
        {
            public Guid From;
            public DoorDirection Dir;
            public Guid To;
        }

        /// <summary>
        /// Conexiones intraversables del grafo. Una arista A→B en <c>dir</c> es válida solo
        /// si A tiene puerta en <c>dir</c> <b>y</b> B tiene puerta en <c>dir.Opposite()</c>
        /// (una puerta asimétrica deja al jugador sin poder volver). Deduplicada por par de
        /// salas — entre dos vecinas 4-adjacentes hay una sola arista.
        /// </summary>
        public static List<DoorlessEdge> ComputeDoorlessEdges(
            IReadOnlyDictionary<Guid, RoomInstance> instances,
            Func<RoomInstance, DoorDirection, bool> hasDoor)
        {
            var result = new List<DoorlessEdge>();
            if (instances == null || hasDoor == null) return result;

            var seen = new HashSet<(Guid, Guid)>();
            foreach (var kvp in instances)
            {
                var a = kvp.Value;
                if (a?.Connections == null) continue;

                foreach (var conn in a.Connections)
                {
                    var dir = conn.Key;
                    var bId = conn.Value;
                    if (!instances.TryGetValue(bId, out var b) || b == null) continue;

                    if (hasDoor(a, dir) && hasDoor(b, dir.Opposite())) continue;

                    if (!seen.Add(OrderedPair(a.InstanceId, bId))) continue;
                    result.Add(new DoorlessEdge { From = a.InstanceId, Dir = dir, To = bId });
                }
            }
            return result;
        }

        /// <summary>
        /// Salas alcanzables desde <paramref name="start"/> por BFS sobre
        /// <see cref="RoomInstance.Connections"/>. Se usa tras podar para detectar salas
        /// que quedaron aisladas (softlock).
        /// </summary>
        public static HashSet<Guid> ReachableFrom(
            Guid start, IReadOnlyDictionary<Guid, RoomInstance> instances)
        {
            var visited = new HashSet<Guid>();
            if (instances == null || !instances.ContainsKey(start)) return visited;

            var queue = new Queue<Guid>();
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var inst = instances[id];
                if (inst?.Connections == null) continue;

                foreach (var conn in inst.Connections)
                {
                    if (instances.ContainsKey(conn.Value) && visited.Add(conn.Value))
                        queue.Enqueue(conn.Value);
                }
            }
            return visited;
        }

        private static (Guid, Guid) OrderedPair(Guid x, Guid y)
            => x.CompareTo(y) <= 0 ? (x, y) : (y, x);
    }
}
