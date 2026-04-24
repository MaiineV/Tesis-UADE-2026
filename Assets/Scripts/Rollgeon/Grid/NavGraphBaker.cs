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

            foreach (var r in renderers)
            {
                var localPos = roomRoot.transform.InverseTransformPoint(r.transform.position);
                int x = Mathf.RoundToInt(localPos.x / tileSize);
                int y = Mathf.RoundToInt(localPos.z / tileSize);
                float height = localPos.y;

                graph.AddNode(new NavNode(new GridCoord(x, y), height));
            }

            var nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (nodes[i].Coord.Manhattan(nodes[j].Coord) != 1) continue;
                    if (Mathf.Abs(nodes[i].Height - nodes[j].Height) > heightThreshold) continue;

                    graph.AddBidirectionalEdge(nodes[i].Coord, nodes[j].Coord, 1f);
                }
            }

            return graph;
        }
    }
}
