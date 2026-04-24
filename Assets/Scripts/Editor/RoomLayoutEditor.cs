using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    [CustomEditor(typeof(RoomLayout))]
    public class RoomLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var layout = (RoomLayout)target;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Bake NavGraph", GUILayout.Height(30)))
            {
                Undo.RecordObject(layout, "Bake NavGraph");
                layout.NavGraph = NavGraphBaker.Bake(layout.gameObject, layout.BakeSettings);
                EditorUtility.SetDirty(layout);

                int nodeCount = layout.NavGraph.NodeCount;
                int edgeCount = layout.NavGraph.Edges?.Count ?? 0;
                Debug.Log($"[NavGraphBaker] Baked {nodeCount} nodes, {edgeCount} edges.");
            }

            if (layout.NavGraph != null && !layout.NavGraph.IsEmpty)
            {
                EditorGUILayout.HelpBox(
                    $"NavGraph: {layout.NavGraph.NodeCount} nodes, {layout.NavGraph.Edges.Count} edges",
                    MessageType.Info);
            }
        }
    }
}
