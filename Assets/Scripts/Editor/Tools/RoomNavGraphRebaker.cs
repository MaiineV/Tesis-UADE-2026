using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Rebakea el NavGraph serializado de todos los prefabs de sala. Necesario
    /// cuando cambia la lógica de <see cref="NavGraphBaker"/> (ej. BUG-012:
    /// bloqueo por Footprint en vez de renderer bounds; BUG-061: nodos aislados
    /// por props apoyados sobre <c>WalkClearance</c>) — los prefabs guardan el
    /// graph baked, no lo recalculan en runtime.
    /// </summary>
    /// <remarks>
    /// BUG-061/BUG-069: además de rebakear, corre un reporte de post-validación
    /// (nodos aislados restantes, componentes desconexas, nodos sin TileMarker
    /// Floor de respaldo, tiles Floor duplicados en la misma celda — estos
    /// últimos se PODAN, el resto solo se reporta) para que el resultado del
    /// batch sea accionable sala por sala, no solo "bakeado sin errores".
    /// </remarks>
    public static class RoomNavGraphRebaker
    {
        // Solo salas vivas. OLD/ tiene prefabs muertos que no deben re-serializarse.
        private static readonly string[] RoomFolders =
        {
            "Assets/Prefabs/Rooms/FloorOne",
            "Assets/Prefabs/Rooms/FloorTwo",
            "Assets/Prefabs/Rooms/FloorThree",
        };

        private static readonly Regex DuplicateSuffix = new Regex(@"\s*\(\d+\)$");

        // Alias del mismo comando: el nombre histórico ("Tools/…") queda por compat de
        // discoverability con quien ya lo tiene en Favorites; "Rooms/…" es el pedido del
        // ticket BUG-061/069.
        [MenuItem("Rollgeon/Tools/Rebake Room NavGraphs")]
        [MenuItem("Rollgeon/Rooms/Rebake All NavGraphs")]
        public static void RebakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", RoomFolders);
            int baked = 0;
            var findings = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null) continue;

                    // Antes se salteaban las salas con NavGraph vacío para no convertir
                    // "sin restricciones" en un graph real sin querer. Pero eso dejaba las
                    // salas nuevas (FloorTwo) sin bakear y con el cruce de puertas roto en
                    // silencio. Ahora se bakean igual, avisando que arrancaban vacías.
                    bool wasEmpty = layout.NavGraph == null || layout.NavGraph.IsEmpty;
                    if (wasEmpty)
                        Debug.LogWarning($"[RoomNavGraphRebaker] {path}: NavGraph vacío (sala nueva sin bakear). Bakeando ahora.");

                    int dupsPruned = PruneDuplicateFloorTiles(root, path, findings);

                    layout.NavGraph = NavGraphBaker.Bake(root, layout.BakeSettings);

                    foreach (var f in RoomDoorBakeValidator.ValidateRoom(layout))
                        findings.Add($"{path}: {f}");

                    ReportIsolatedNodes(layout.NavGraph, path, findings);
                    ReportDisconnectedComponents(layout.NavGraph, path, findings);
                    ReportNodesWithoutFloorBacking(root, layout, path, findings);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    baked++;
                    Debug.Log($"[RoomNavGraphRebaker] {path}: {layout.NavGraph.NodeCount} nodes, " +
                              $"{layout.NavGraph.Edges.Count} edges" +
                              (dupsPruned > 0 ? $", {dupsPruned} duplicate tile(s) pruned" : "") + ".");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomNavGraphRebaker] Rebaked {baked} room prefabs.");
            if (findings.Count > 0)
                Debug.LogWarning(
                    $"[RoomNavGraphRebaker] {findings.Count} finding(s) tras el rebake:\n• " +
                    string.Join("\n• ", findings));
            else
                Debug.Log("[RoomNavGraphRebaker] Sin findings — todas las salas quedaron con NavGraph consistente.");
        }

        // -----------------------------------------------------------------
        // Duplicados — Combat_Room_FloorTwo02 (177 tiles / 169 celdas)
        // -----------------------------------------------------------------

        /// <summary>
        /// Dos (o más) <see cref="TileMarker"/> Floor en la misma celda con el mismo nombre
        /// base (Unity sufija duplicados como "Foo (1)") son el mismo tile pisándose — deja
        /// uno (el primero en orden de jerarquía) y destruye el resto ANTES de bakear, así el
        /// grafo resultante no arrastra el fantasma.
        /// </summary>
        private static int PruneDuplicateFloorTiles(GameObject root, string path, List<string> findings)
        {
            var markers = root.GetComponentsInChildren<TileMarker>(true);
            var seen = new Dictionary<(GridCoord, string), TileMarker>();
            var toDestroy = new List<TileMarker>();

            foreach (var m in markers)
            {
                if (m == null || m.Type != TileType.Floor) continue;
                var key = (m.Coord, DuplicateSuffix.Replace(m.gameObject.name, string.Empty));
                if (seen.TryGetValue(key, out var first))
                {
                    toDestroy.Add(m);
                    findings.Add($"{path}: celda {m.Coord} — tile duplicado '{m.gameObject.name}' " +
                                 $"podado (se conserva '{first.gameObject.name}').");
                }
                else
                {
                    seen[key] = m;
                }
            }

            foreach (var m in toDestroy)
                Object.DestroyImmediate(m.gameObject);

            return toDestroy.Count;
        }

        // -----------------------------------------------------------------
        // Reporte — no auto-corrige nada de acá para abajo, solo informa
        // -----------------------------------------------------------------

        /// <summary>Nodos con grado 0 que sobrevivieron el pruning del baker (pockets
        /// legítimos de 1 celda, o un caso que el criterio del baker no cubre).</summary>
        private static void ReportIsolatedNodes(NavGraph graph, string path, List<string> findings)
        {
            if (graph == null || graph.IsEmpty) return;
            foreach (var node in graph.Nodes)
            {
                if (HasAnyNeighbor(graph, node.Coord)) continue;
                findings.Add($"{path}: nodo aislado (grado 0) en {node.Coord} — revisar a mano " +
                             "(pocket legítimo o blocker que el baker no detectó).");
            }
        }

        /// <summary>BFS sobre la adjacencia del NavGraph: componentes ≠ la más grande son
        /// "bolsones" desconectados del resto de la sala (ej. Combat_Room03).</summary>
        private static void ReportDisconnectedComponents(NavGraph graph, string path, List<string> findings)
        {
            if (graph == null || graph.IsEmpty) return;

            var visited = new HashSet<GridCoord>();
            var components = new List<List<GridCoord>>();

            foreach (var node in graph.Nodes)
            {
                if (visited.Contains(node.Coord)) continue;

                var component = new List<GridCoord>();
                var queue = new Queue<GridCoord>();
                queue.Enqueue(node.Coord);
                visited.Add(node.Coord);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    foreach (var edge in graph.GetNeighbors(current))
                    {
                        if (visited.Contains(edge.To)) continue;
                        visited.Add(edge.To);
                        queue.Enqueue(edge.To);
                    }
                }

                components.Add(component);
            }

            if (components.Count <= 1) return;

            components.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (int i = 1; i < components.Count; i++)
            {
                var coords = string.Join(", ", components[i]);
                findings.Add($"{path}: componente desconexa de {components[i].Count} celda(s) " +
                             $"aislada del resto de la sala: {coords}.");
            }
        }

        /// <summary>Nodo horneado sin ningún TileMarker Floor en esa celda: sale de un mesh
        /// legacy inferido por posición (BUG-061 item 2 — 10 nodos de X=6 dentro de la pared
        /// este de Combat_Room_FloorTwo02) o de autoría fuera del bbox esperado.</summary>
        private static void ReportNodesWithoutFloorBacking(
            GameObject root, RoomLayout layout, string path, List<string> findings)
        {
            var graph = layout.NavGraph;
            if (graph == null || graph.IsEmpty) return;

            var floorCoords = new HashSet<GridCoord>();
            foreach (var m in root.GetComponentsInChildren<TileMarker>(true))
                if (m != null && m.Type == TileType.Floor && !m.IsBlocker)
                    floorCoords.Add(m.Coord);

            // Salas sin TileMarker en absoluto (legacy total, todo por Renderer inferido) no
            // tienen contra qué comparar — nada que reportar acá, sería 100% falsos positivos.
            if (floorCoords.Count == 0) return;

            foreach (var node in graph.Nodes)
            {
                if (floorCoords.Contains(node.Coord)) continue;
                findings.Add($"{path}: nodo en {node.Coord} sin TileMarker Floor de respaldo " +
                             "— probable mesh legacy inferido o autoría fuera del bbox esperado.");
            }
        }

        private static bool HasAnyNeighbor(NavGraph graph, GridCoord coord)
        {
            foreach (var _ in graph.GetNeighbors(coord)) return true;
            return false;
        }
    }
}
