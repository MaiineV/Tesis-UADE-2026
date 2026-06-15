using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    [CustomEditor(typeof(RoomLayout))]
    public class RoomLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var layout = (RoomLayout)target;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Auto-Populate Door Slots", GUILayout.Height(30)))
            {
                layout.AutoPopulateDoorSlots();
            }

            if (GUILayout.Button("Bake NavGraph", GUILayout.Height(30)))
            {
                Undo.RecordObject(layout, "Bake NavGraph");
                layout.NavGraph = NavGraphBaker.Bake(layout.gameObject, layout.BakeSettings);
                EditorUtility.SetDirty(layout);

                int nodeCount = layout.NavGraph.NodeCount;
                int edgeCount = layout.NavGraph.Edges?.Count ?? 0;
                Debug.Log($"[NavGraphBaker] Baked {nodeCount} nodes, {edgeCount} edges.");

                ValidateTiles(layout); // surface tile-authoring problems right after baking
            }

            if (GUILayout.Button("Validate Tiles", GUILayout.Height(24)))
                ValidateTiles(layout);

            if (layout.NavGraph != null && !layout.NavGraph.IsEmpty)
            {
                EditorGUILayout.HelpBox(
                    $"NavGraph: {layout.NavGraph.NodeCount} nodes, {layout.NavGraph.Edges.Count} edges",
                    MessageType.Info);
            }
        }

        // Detecta la clase de error que rompía el highlight: dos (o más) TileMarker
        // Type=Floor en la misma celda. _tileRenderers hace "último gana", así que un
        // marker extra (un prop mal tipado como Floor, o con su Coord sin autorar →
        // default (0,0)) le roba el slot pintable al piso real y ese tile queda
        // caminable pero sin highlight. Corre en autoría, sin Play.
        private static void ValidateTiles(RoomLayout layout)
        {
            var markers = layout.GetComponentsInChildren<TileMarker>(true);
            var byCoord = new Dictionary<GridCoord, List<TileMarker>>();
            foreach (var m in markers)
            {
                if (m == null) continue;
                if (!byCoord.TryGetValue(m.Coord, out var list))
                {
                    list = new List<TileMarker>();
                    byCoord[m.Coord] = list;
                }
                list.Add(m);
            }

            int dupCells = 0;
            foreach (var kv in byCoord)
            {
                int floors = 0;
                foreach (var m in kv.Value)
                    if (m.Type == TileType.Floor) floors++;
                if (floors < 2) continue;

                dupCells++;
                var names = new List<string>();
                foreach (var m in kv.Value)
                    names.Add($"{m.gameObject.name}({m.Type})");
                Debug.LogWarning(
                    $"[RoomLayout] '{layout.name}': coord {kv.Key} con {floors} markers Type=Floor: " +
                    $"{string.Join(", ", names)}. Se pisan el renderer pintable (highlight). " +
                    $"Si alguno es prop/pared cambiá su Type; si su Coord es (0,0) probablemente quedó sin autorar.",
                    layout);
            }

            if (dupCells == 0)
                Debug.Log($"[RoomLayout] '{layout.name}': tiles OK — sin celdas con Floor duplicado.", layout);
            else
                Debug.LogWarning($"[RoomLayout] '{layout.name}': {dupCells} celda(s) con Floor duplicado (ver detalle arriba).", layout);
        }
    }
}
