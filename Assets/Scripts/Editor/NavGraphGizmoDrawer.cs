using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    public static class NavGraphGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawNavGraphGizmos(RoomLayout layout, GizmoType gizmoType)
        {
            if (layout == null || layout.NavGraph == null || layout.NavGraph.IsEmpty) return;

            float tileSize = layout.TileSize;
            Vector3 origin = layout.GetOrigin();
            var graph = layout.NavGraph;

            Gizmos.color = Color.green;
            foreach (var node in graph.Nodes)
            {
                Vector3 worldPos = origin + new Vector3(
                    node.Coord.X * tileSize, node.Height, node.Coord.Y * tileSize);
                Gizmos.DrawSphere(worldPos, tileSize * 0.15f);
                Handles.Label(worldPos + Vector3.up * 0.3f, $"h={node.Height:F1}");
            }

            Gizmos.color = new Color(0f, 0.8f, 0f, 0.6f);
            foreach (var edge in graph.Edges)
            {
                if (!graph.TryGetNode(edge.From, out var fromNode)) continue;
                if (!graph.TryGetNode(edge.To, out var toNode)) continue;

                Vector3 fromWorld = origin + new Vector3(
                    fromNode.Coord.X * tileSize, fromNode.Height, fromNode.Coord.Y * tileSize);
                Vector3 toWorld = origin + new Vector3(
                    toNode.Coord.X * tileSize, toNode.Height, toNode.Coord.Y * tileSize);

                Gizmos.DrawLine(fromWorld, toWorld);
            }
        }
    }
}
