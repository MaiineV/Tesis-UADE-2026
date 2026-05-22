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

            // World-space AABB of every tile. Any tile can obstruct another:
            // a tile is walkable only when nothing is stacked on its surface.
            var tiles = new List<(TileMarker marker, Bounds bounds)>();
            foreach (var m in markers)
                if (TryComputeBounds(m.gameObject, out var b))
                    tiles.Add((m, b));

            // Tiles that yield no walkable node (walls, or anything with a tile
            // stacked on top). Their AABBs block edges passing through them.
            var blockerBounds = new List<Bounds>();
            var walkable = new HashSet<TileMarker>();
            foreach (var (m, b) in tiles)
            {
                if (m.Type == TileType.Wall || IsSurfaceBlocked(tiles, m, b))
                    blockerBounds.Add(b);
                else
                    walkable.Add(m);
            }

            var nodeWorldPos = new Dictionary<GridCoord, Vector3>();

            // Walkable markers become nodes at their geometric centre.
            foreach (var (m, b) in tiles)
            {
                if (!walkable.Contains(m)) continue;
                var worldPos = b.center;
                float height = roomRoot.transform.InverseTransformPoint(worldPos).y;
                graph.AddNode(new NavNode(m.Coord, height));
                if (!nodeWorldPos.ContainsKey(m.Coord))
                    nodeWorldPos[m.Coord] = worldPos;
            }

            // Legacy meshes without a TileMarker: infer the cell from position.
            foreach (var r in renderers)
            {
                if (r.GetComponentInParent<TileMarker>() != null) continue;
                var worldPos = r.bounds.center;
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

        // True when another tile rests on top of 'self': its AABB centre sits
        // above self's and overlaps at least half of the smaller tile's
        // horizontal area. Rotation/footprint agnostic — pure world geometry.
        private static bool IsSurfaceBlocked(
            List<(TileMarker marker, Bounds bounds)> tiles, TileMarker self, Bounds selfBounds)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                var (m, b) = tiles[i];
                if (m == self) continue;
                if (b.center.y <= selfBounds.center.y) continue;

                float ox = Mathf.Min(b.max.x, selfBounds.max.x) - Mathf.Max(b.min.x, selfBounds.min.x);
                float oz = Mathf.Min(b.max.z, selfBounds.max.z) - Mathf.Max(b.min.z, selfBounds.min.z);
                if (ox <= 0f || oz <= 0f) continue;

                float overlap = ox * oz;
                float smaller = Mathf.Min(b.size.x * b.size.z, selfBounds.size.x * selfBounds.size.z);
                if (smaller > 0f && overlap >= smaller * 0.5f) return true;
            }
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
