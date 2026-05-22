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
                Vector3 worldPos = NodeWorld(node, origin, tileSize);
                Gizmos.DrawSphere(worldPos, tileSize * 0.15f);
                Handles.Label(worldPos + Vector3.up * 0.3f, $"h={node.Height:F1}");
            }

            Gizmos.color = new Color(0f, 0.8f, 0f, 0.6f);
            foreach (var edge in graph.Edges)
            {
                if (!graph.TryGetNode(edge.From, out var fromNode)) continue;
                if (!graph.TryGetNode(edge.To, out var toNode)) continue;

                Gizmos.DrawLine(
                    NodeWorld(fromNode, origin, tileSize),
                    NodeWorld(toNode, origin, tileSize));
            }
        }

        // Cells are corner-anchored: coord (x,y) spans [x, x+1] in tile units,
        // so the cell centre sits at +0.5 on each planar axis.
        private static Vector3 NodeWorld(NavNode node, Vector3 origin, float tileSize)
        {
            return origin + new Vector3(
                (node.Coord.X + 0.5f) * tileSize,
                node.Height,
                (node.Coord.Y + 0.5f) * tileSize);
        }
    }
}
