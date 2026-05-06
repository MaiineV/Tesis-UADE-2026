using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Simple tree layout: depth × X spacing, sibling index × Y spacing. Doesn't account
    /// for subtree width — that would need Reingold–Tilford. The graph view supports manual
    /// drag, so this just gives a non-overlapping starting point.
    /// </summary>
    public static class AITreeAutoLayout
    {
        const float X_SPACING = 280f;
        const float Y_SPACING = 160f;

        public static Dictionary<AIDecisionNode, Vector2> Compute(GraphSnapshot snap)
        {
            var positions = new Dictionary<AIDecisionNode, Vector2>();
            if (snap?.Root == null) return positions;

            int siblingCounter = 0;
            Walk(snap, snap.Root, depth: 0, ref siblingCounter, positions);
            return positions;
        }

        static void Walk(GraphSnapshot snap, AIDecisionNode node, int depth, ref int siblingCounter, Dictionary<AIDecisionNode, Vector2> positions)
        {
            if (node == null || positions.ContainsKey(node)) return;
            positions[node] = new Vector2(depth * X_SPACING, siblingCounter * Y_SPACING);
            siblingCounter++;

            foreach (var e in snap.Edges)
            {
                if (e.Parent != node) continue;
                Walk(snap, e.Child, depth + 1, ref siblingCounter, positions);
            }
        }
    }

    /// <summary>
    /// Sidecar JSON layout persistence keyed by pre-order traversal index. Stable as long as
    /// the tree topology doesn't change; falls back to auto-layout when keys mismatch.
    /// </summary>
    public static class AITreeLayoutSidecar
    {
        const string LAYOUTS_DIR = "Assets/Rollgeon/Enemies/_layouts";

        [System.Serializable]
        sealed class LayoutFile
        {
            public List<Entry> Entries = new List<Entry>();

            [System.Serializable]
            public sealed class Entry
            {
                public int Index;
                public string TypeName;
                public Vector2 Position;
            }
        }

        public static Dictionary<AIDecisionNode, Vector2> Load(EnemyDataSO so, GraphSnapshot snap)
        {
            string path = PathFor(so);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<LayoutFile>(json);
                if (data?.Entries == null) return null;

                var ordered = PreOrder(snap);
                var result = new Dictionary<AIDecisionNode, Vector2>();
                foreach (var entry in data.Entries)
                {
                    if (entry.Index < 0 || entry.Index >= ordered.Count) continue;
                    var node = ordered[entry.Index];
                    if (node == null) continue;
                    if (node.GetType().Name != entry.TypeName) continue; // topology drift
                    result[node] = entry.Position;
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(EnemyDataSO so, GraphSnapshot snap, Dictionary<AIDecisionNode, Vector2> positions)
        {
            if (so == null || positions == null) return;
            Directory.CreateDirectory(LAYOUTS_DIR);

            var ordered = PreOrder(snap);
            var data = new LayoutFile();
            for (int i = 0; i < ordered.Count; i++)
            {
                var n = ordered[i];
                if (n == null || !positions.TryGetValue(n, out var pos)) continue;
                data.Entries.Add(new LayoutFile.Entry
                {
                    Index = i,
                    TypeName = n.GetType().Name,
                    Position = pos,
                });
            }

            // Sidecar lives on disk only; we never read it via AssetDatabase, so skip
            // the Refresh — calling it in the middle of an edit caused field values to
            // round-trip through Unity's importer and revert in-memory edits.
            File.WriteAllText(PathFor(so), JsonUtility.ToJson(data, prettyPrint: true));
        }

        static string PathFor(EnemyDataSO so)
        {
            string id = string.IsNullOrEmpty(so.EntityId) ? so.name : so.EntityId;
            return Path.Combine(LAYOUTS_DIR, $"{id}.json").Replace('\\', '/');
        }

        static List<AIDecisionNode> PreOrder(GraphSnapshot snap)
        {
            var list = new List<AIDecisionNode>();
            if (snap?.Root == null) return list;
            var visited = new HashSet<AIDecisionNode>();
            Visit(snap, snap.Root, visited, list);
            return list;
        }

        static void Visit(GraphSnapshot snap, AIDecisionNode node, HashSet<AIDecisionNode> visited, List<AIDecisionNode> list)
        {
            if (node == null || !visited.Add(node)) return;
            list.Add(node);
            foreach (var e in snap.Edges)
            {
                if (e.Parent == node) Visit(snap, e.Child, visited, list);
            }
        }
    }
}
