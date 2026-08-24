using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
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

            // Origin del grid autorado. Los blockers ubican su footprint en
            // espacio de celdas (Coord + FootprintOffset) relativo a este
            // origin, NO al pivot del prop — que puede estar desfasado. Sin
            // RoomLayout (tests, roots sueltos) cae al transform del root.
            var layout = roomRoot.GetComponent<RoomLayout>();
            Vector3 gridOrigin = layout != null ? layout.GetOrigin() : roomRoot.transform.position;

            var graph = new NavGraph();
            var renderers = roomRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            var markers = roomRoot.GetComponentsInChildren<TileMarker>(includeInactive: false);

            var tiles = new List<(TileMarker marker, Bounds bounds)>();
            foreach (var m in markers)
                if (TryComputeBounds(m.gameObject, out var b))
                    tiles.Add((m, b));

            // IsBlocker is the only source of truth. Stacking is allowed for
            // non-blockers and never auto-promotes them to obstacles.
            // BUG-012: el volumen horizontal de bloqueo sale del Footprint
            // autorado por la tool (en celdas), NO del renderer del modelo —
            // un prop de 1 celda con un mesh que sobresale bloqueaba 2-3
            // celdas al moverse.
            var blockerBounds = new List<Bounds>();
            foreach (var m in markers)
                if (m.IsBlocker) blockerBounds.Add(BlockerBounds(m, gridOrigin, tileSize));

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

            RemoveBlockedIsolatedNodes(graph, blockerBounds, gridOrigin, tileSize);

            return graph;
        }

        // BUG-061: el nodo sobrevive si el blocker entra en la banda de walk-clearance
        // (IntersectsAnyBlocker, arriba), pero el CORTE de edges usa el AABB completo del
        // blocker (IsSegmentBlocked) sin ese recorte de Y — un prop apoyado por encima de
        // WalkClearance (ej. un barril a Y=1.0) deja el nodo "caminable" con sus 4 edges
        // cortados. Ese enemigo queda en una isla de 1 celda para siempre.
        //
        // Criterio unificado post-pass: un nodo con grado 0 cuya celda (en XZ, CUALQUIER Y)
        // solapa el footprint de un blocker real no existe — mismo estándar que
        // IntersectsAnyBlocker pero sin la banda de altura, porque acá ya no importa si el
        // blocker "vuela" sobre el piso: si mató todos los edges, el nodo es inalcanzable.
        // Un nodo con grado 0 SIN blocker superpuesto (sala vacía, pockets legítimos)
        // no se toca — esos son responsabilidad del validador (componentes desconexas), no
        // de este pruning.
        private static void RemoveBlockedIsolatedNodes(
            NavGraph graph, List<Bounds> blockers, Vector3 origin, float tileSize)
        {
            if (blockers.Count == 0 || graph.IsEmpty) return;

            var toRemove = new List<GridCoord>();
            foreach (var node in graph.Nodes)
            {
                if (HasAnyNeighbor(graph, node.Coord)) continue;
                if (!CellOverlapsAnyBlockerXZ(node.Coord, blockers, origin, tileSize)) continue;
                toRemove.Add(node.Coord);
            }

            foreach (var coord in toRemove) graph.RemoveNode(coord);
        }

        private static bool HasAnyNeighbor(NavGraph graph, GridCoord coord)
        {
            foreach (var _ in graph.GetNeighbors(coord)) return true;
            return false;
        }

        private static bool CellOverlapsAnyBlockerXZ(
            GridCoord coord, List<Bounds> blockers, Vector3 origin, float tileSize)
        {
            float minX = origin.x + coord.X * tileSize;
            float maxX = minX + tileSize;
            float minZ = origin.z + coord.Y * tileSize;
            float maxZ = minZ + tileSize;

            for (int i = 0; i < blockers.Count; i++)
            {
                var wb = blockers[i];
                float ox = Mathf.Min(maxX, wb.max.x) - Mathf.Max(minX, wb.min.x);
                float oz = Mathf.Min(maxZ, wb.max.z) - Mathf.Max(minZ, wb.min.z);
                if (ox > BlockerOverlapEpsilon && oz > BlockerOverlapEpsilon) return true;
            }
            return false;
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

        // Región de bloqueo de un marker. En XZ son las celdas del Footprint
        // ubicadas por el espacio de celdas autorado: minCorner = origin +
        // (Coord + FootprintOffset) * tileSize. Es la MISMA región que evalúa
        // el overlap del RoomEditor (mMin = Coord+off, mMax = mMin+fp) y es
        // inmune al pivot del prop: un 2x2 cuyo pivot está corrido del centro
        // del mesh bloqueaba celdas desfasadas cuando la región salía de
        // transform.position. En Y se conserva el rango del renderer cuando
        // existe — es lo que decide si el blocker llega a la banda de walk
        // clearance — con fallback al volumen de celdas autorado.
        private static Bounds BlockerBounds(TileMarker marker, Vector3 origin, float tileSize)
        {
            var coord = marker.Coord;
            var fp = marker.Footprint;
            var off = marker.FootprintOffset;

            float sizeX = Mathf.Max(1, fp.x) * tileSize;
            float sizeZ = Mathf.Max(1, fp.z) * tileSize;
            float minX = origin.x + (coord.X + off.x) * tileSize;
            float minZ = origin.z + (coord.Y + off.z) * tileSize;

            float minY, sizeY;
            if (TryComputeBounds(marker.gameObject, out var rendered))
            {
                minY = rendered.min.y;
                sizeY = rendered.size.y;
            }
            else
            {
                sizeY = Mathf.Max(1, fp.y) * tileSize;
                minY = origin.y + (marker.Layer + off.y) * tileSize;
            }

            var size = new Vector3(sizeX, sizeY, sizeZ);
            var center = new Vector3(minX, minY, minZ) + size * 0.5f;
            return new Bounds(center, size);
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
