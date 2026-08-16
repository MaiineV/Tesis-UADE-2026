using System.Text;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Vuelca a consola el mapa caminable de una sala de jefe ya construida
    /// (<c>Rollgeon → Bosses → Dump Boss Room Maps</c>). Herramienta de diagnóstico: los findings del
    /// builder dicen <i>qué</i> celda está mal, pero no dejan ver la forma del problema.
    /// </summary>
    /// <remarks>
    /// Nació persiguiendo el mostrador del Cajero, donde cuatro findings distintos (una celda aislada,
    /// un blocker que no bloquea y dos puertas sin tile-frente) resultaron ser la misma cosa vista
    /// desde cuatro lados. Con el mapa impreso se ve de una.
    /// </remarks>
    public static class BossRoomDiagnostics
    {
        private const string LogPrefix = "[BossRoomDiagnostics] ";

        [MenuItem("Rollgeon/Bosses/Dump Boss Room Maps")]
        public static void DumpAll()
        {
            foreach (var plan in BossRoomBuilder.Plans) Dump(plan);
        }

        private static void Dump(BossRoomPlan plan)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plan.OutputRoomPath);
            if (prefab == null)
            {
                Debug.LogWarning(LogPrefix + $"{plan.BossName}: no está '{plan.OutputRoomPath}'.");
                return;
            }

            var layout = prefab.GetComponent<RoomLayout>();
            if (layout == null || layout.NavGraph == null)
            {
                Debug.LogWarning(LogPrefix + $"{plan.BossName}: la sala no tiene RoomLayout con NavGraph.");
                return;
            }

            var graph = layout.NavGraph;
            var walkable = new System.Collections.Generic.HashSet<GridCoord>();
            foreach (var node in graph.Nodes) walkable.Add(node.Coord);

            // Grados por nodo: una casilla caminable con 0 aristas es una isla, y es un modo de falla
            // que el mapa de "caminable sí/no" solo no muestra.
            var degree = new System.Collections.Generic.Dictionary<GridCoord, int>();
            foreach (var edge in graph.Edges)
            {
                degree.TryGetValue(edge.From, out int from);
                degree[edge.From] = from + 1;
                degree.TryGetValue(edge.To, out int to);
                degree[edge.To] = to + 1;
            }

            // Una línea de log por fila y no un bloque multi-línea: la consola de Unity (y cualquier
            // lector externo del log) se queda con la primera línea del mensaje, así que un mapa
            // dibujado en un solo Debug.Log es exactamente el mapa que no se puede leer.
            Debug.Log(LogPrefix + $"{plan.BossName} — '{plan.OutputRoomPath}' " +
                      "( . caminable | # bloqueado | ! caminable pero AISLADA )");

            for (int y = 5; y >= -5; y--)
            {
                var sb = new StringBuilder();
                sb.Append(LogPrefix).Append($"{plan.BossName} y={y,3} |");
                for (int x = -5; x <= 5; x++)
                {
                    var cell = new GridCoord(x, y);
                    if (!walkable.Contains(cell)) sb.Append('#');
                    else if (!degree.TryGetValue(cell, out int d) || d == 0) sb.Append('!');
                    else sb.Append('.');
                }
                sb.Append("|  (x = -5..5)");
                Debug.Log(sb.ToString());
            }
        }
    }
}
