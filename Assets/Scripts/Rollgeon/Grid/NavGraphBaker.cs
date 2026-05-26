using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    public static class NavGraphBaker
    {
        public static NavGraph Bake(GameObject roomRoot, NavGraphBakeSettings settings)
        {
            if (roomRoot == null || settings == null) return new NavGraph();

            float tileSize = Mathf.Max(settings.TileSize, 0.01f);
            float heightThreshold = Mathf.Max(settings.HeightThreshold, 0f);

            var graph = new NavGraph();
            var renderers = roomRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            var markers = roomRoot.GetComponentsInChildren<TileMarker>(includeInactive: false);

            var tiles = new List<(TileMarker marker, Bounds bounds)>();
            foreach (var m in markers)
                if (TryComputeBounds(m.gameObject, out var b))
                    tiles.Add((m, b));

            // IsBlocker is the only source of truth. Stacking is allowed for
            // non-blockers and never auto-promotes them to obstacles.
            var blockerBounds = new List<Bounds>();
            foreach (var (m, b) in tiles)
                if (m.IsBlocker) blockerBounds.Add(b);

            var nodeWorldPos = new Dictionary<GridCoord, Vector3>();

            // Walkable nodes come from Floor tiles that aren't blockers and
            // aren't swallowed by an overlapping blocker. Decorations, doors
            // and interactables ride atop the floor below and add no node.
            foreach (var (m, b) in tiles)
            {
                if (m.IsBlocker) continue;
                if (m.Type != TileType.Floor) continue;
                if (IntersectsAnyBlocker(b, blockerBounds)) continue;

                var worldPos = b.center;
                float height = roomRoot.transform.InverseTransformPoint(worldPos).y;
                graph.AddNode(new NavNode(m.Coord, height));
                if (!nodeWorldPos.ContainsKey(m.Coord))
                    nodeWorldPos[m.Coord] = worldPos;
            }

            // Legacy meshes without a TileMarker: infer the cell from position
            // and treat them as walkable surfaces. They never block, and they
            // are ignored if their centre falls inside a blocker volume.
            foreach (var r in renderers)
            {
                if (r.GetComponentInParent<TileMarker>() != null) continue;
                var worldPos = r.bounds.center;
                if (IsInsideAnyBlocker(worldPos, blockerBounds)) continue;
                var lp = roomRoot.transform.InverseTransformPoint(worldPos);
                var coord = new GridCoord(
                    Mathf.FloorToInt(lp.x / tileSize),
                    Mathf.FloorToInt(lp.z / tileSize));
                graph.AddNode(new NavNode(coord, lp.y));
                if (!nodeWorldPos.ContainsKey(coord))
                    nodeWorldPos[coord] = worldPos;
            }

            var nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (nodes[i].Coord.Manhattan(nodes[j].Coord) != 1) continue;
                    if (Mathf.Abs(nodes[i].Height - nodes[j].Height) > heightThreshold) continue;

                    if (!nodeWorldPos.TryGetValue(nodes[i].Coord, out var pa)) continue;
                    if (!nodeWorldPos.TryGetValue(nodes[j].Coord, out var pb)) continue;
                    if (IsSegmentBlocked(blockerBounds, pa, pb)) continue;

                    graph.AddBidirectionalEdge(nodes[i].Coord, nodes[j].Coord, 1f);
                }
            }

            return graph;
        }

        private const float BlockerOverlapEpsilon = 0.01f;

        // Headroom above a floor that a character is assumed to occupy. Any
        // blocker reaching into that band kills the floor's walkable node,
        // even when the blocker just sits on top of the floor (shared face,
        // zero volumetric overlap).
        private const float WalkClearance = 0.5f;

        private static bool IntersectsAnyBlocker(Bounds floorBounds, List<Bounds> blockers)
        {
            float topY = floorBounds.max.y;
            float walkTopY = topY + WalkClearance;
            for (int i = 0; i < blockers.Count; i++)
            {
                var wb = blockers[i];
                float ox = Mathf.Min(floorBounds.max.x, wb.max.x) - Mathf.Max(floorBounds.min.x, wb.min.x);
                float oz = Mathf.Min(floorBounds.max.z, wb.max.z) - Mathf.Max(floorBounds.min.z, wb.min.z);
                if (ox <= BlockerOverlapEpsilon || oz <= BlockerOverlapEpsilon) continue;
                if (wb.max.y <= topY + BlockerOverlapEpsilon) continue;   // blocker entirely below floor top
                if (wb.min.y >= walkTopY) continue;                       // blocker entirely above walk volume
                return true;
            }
            return false;
        }

        private static bool IsInsideAnyBlocker(Vector3 point, List<Bounds> blockers)
        {
            for (int i = 0; i < blockers.Count; i++)
                if (blockers[i].Contains(point)) return true;
            return false;
        }

        private static bool TryComputeBounds(GameObject go, out Bounds bounds)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers.Length == 0) { bounds = default; return false; }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static bool IsSegmentBlocked(List<Bounds> blockers, Vector3 a, Vector3 b)
        {
            var dir = b - a;
            float dist = dir.magnitude;
            if (dist < 0.001f) return false;
            var ray = new Ray(a, dir / dist);
            for (int i = 0; i < blockers.Count; i++)
            {
                var wb = blockers[i];
                if (wb.Contains(a) || wb.Contains(b)) return true;
                if (wb.IntersectRay(ray, out var d) && d <= dist) return true;
            }
            return false;
        }
    }
}
