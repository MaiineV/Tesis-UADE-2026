using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Grafo de navegación sobre la grilla de combate.
    /// Nodos representan tiles transitables, edges representan conexiones con costo.
    /// </summary>
    [Serializable]
    public class NavGraph
    {
        [SerializeField] private List<NavNode> _nodes = new List<NavNode>();
        [SerializeField] private List<NavEdge> _edges = new List<NavEdge>();

        [NonSerialized] private Dictionary<GridCoord, NavNode> _nodeMap;
        [NonSerialized] private Dictionary<GridCoord, List<NavEdge>> _adjacency;
        [NonSerialized] private bool _dirty = true;

        // ── Properties ──────────────────────────────────────────────

        public bool IsEmpty => _nodes.Count == 0;
        public int NodeCount => _nodes.Count;
        public IReadOnlyList<NavNode> Nodes => _nodes;
        public IReadOnlyList<NavEdge> Edges => _edges;

        public int Width
        {
            get
            {
                if (_nodes.Count == 0) return 0;
                int max = 0;
                foreach (var n in _nodes)
                    if (n.Coord.X + 1 > max) max = n.Coord.X + 1;
                return max;
            }
        }

        public int Height
        {
            get
            {
                if (_nodes.Count == 0) return 0;
                int max = 0;
                foreach (var n in _nodes)
                    if (n.Coord.Y + 1 > max) max = n.Coord.Y + 1;
                return max;
            }
        }

        // ── Query ────────────────────────────────────────────────────

        public bool HasNode(GridCoord coord)
        {
            if (IsEmpty) return true;
            EnsureLookups();
            return _nodeMap.ContainsKey(coord);
        }

        public bool TryGetNode(GridCoord coord, out NavNode node)
        {
            if (IsEmpty)
            {
                node = new NavNode(coord);
                return true;
            }
            EnsureLookups();
            return _nodeMap.TryGetValue(coord, out node);
        }

        public IEnumerable<NavEdge> GetNeighbors(GridCoord coord)
        {
            if (IsEmpty)
            {
                foreach (var n in coord.Neighbors4())
                    yield return new NavEdge(coord, n, 1f);
                yield break;
            }
            EnsureLookups();
            if (_adjacency.TryGetValue(coord, out var list))
            {
                foreach (var e in list)
                    yield return e;
            }
        }

        public bool HasEdge(GridCoord from, GridCoord to)
        {
            if (IsEmpty) return false;
            EnsureLookups();
            if (!_adjacency.TryGetValue(from, out var list)) return false;
            foreach (var e in list)
                if (e.To.Equals(to)) return true;
            return false;
        }

        public float GetEdgeCost(GridCoord from, GridCoord to)
        {
            if (IsEmpty) return float.PositiveInfinity;
            EnsureLookups();
            if (_adjacency.TryGetValue(from, out var list))
            {
                foreach (var e in list)
                    if (e.To.Equals(to)) return e.Cost;
            }
            return float.PositiveInfinity;
        }

        public bool InBounds(GridCoord coord)
        {
            if (IsEmpty) return true;
            return HasNode(coord);
        }

        public IEnumerable<GridCoord> AllCoords()
        {
            foreach (var n in _nodes)
                yield return n.Coord;
        }

        // ── Mutation ─────────────────────────────────────────────────

        public void AddNode(NavNode node)
        {
            EnsureLookups();
            if (_nodeMap.ContainsKey(node.Coord)) return;
            _nodes.Add(node);
            _dirty = true;
        }

        public void AddEdge(NavEdge edge)
        {
            _edges.Add(edge);
            _dirty = true;
        }

        public void AddBidirectionalEdge(GridCoord a, GridCoord b, float cost = 1f)
        {
            AddEdge(new NavEdge(a, b, cost));
            AddEdge(new NavEdge(b, a, cost));
        }

        public void RemoveEdge(GridCoord from, GridCoord to)
        {
            _edges.RemoveAll(e => e.From.Equals(from) && e.To.Equals(to));
            _dirty = true;
        }

        public void RemoveBidirectionalEdge(GridCoord a, GridCoord b)
        {
            RemoveEdge(a, b);
            RemoveEdge(b, a);
        }

        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
            _dirty = true;
        }

        // ── Factory ─────────────────────────────────────────────────

        public static NavGraph FromSnapshot(GridSnapshot snapshot)
        {
            var graph = new NavGraph();
            if (snapshot.IsEmpty) return graph;

            foreach (var c in snapshot.AllCoords())
            {
                if (!snapshot.IsWalkable(c)) continue;
                graph._nodes.Add(new NavNode(c));
            }

            foreach (var c in snapshot.AllCoords())
            {
                if (!snapshot.IsWalkable(c)) continue;
                foreach (var n in c.Neighbors4())
                {
                    if (snapshot.InBounds(n) && snapshot.IsWalkable(n))
                        graph._edges.Add(new NavEdge(c, n, 1f));
                }
            }

            graph._dirty = true;
            return graph;
        }

        public static NavGraph Rect(int width, int height)
        {
            return FromSnapshot(GridSnapshot.Rect(width, height));
        }

        // ── Private ─────────────────────────────────────────────────

        private void EnsureLookups()
        {
            if (!_dirty && _nodeMap != null && _adjacency != null) return;

            _nodeMap = new Dictionary<GridCoord, NavNode>(_nodes.Count);
            foreach (var n in _nodes)
                _nodeMap[n.Coord] = n;

            _adjacency = new Dictionary<GridCoord, List<NavEdge>>();
            foreach (var e in _edges)
            {
                if (!_adjacency.TryGetValue(e.From, out var list))
                {
                    list = new List<NavEdge>();
                    _adjacency[e.From] = list;
                }
                list.Add(e);
            }

            _dirty = false;
        }
    }
}
