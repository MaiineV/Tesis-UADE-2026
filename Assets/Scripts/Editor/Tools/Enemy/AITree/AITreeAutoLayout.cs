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
    /// drag, so this just gives a non-overlapping starting point. Los subárboles sueltos se
    /// apilan debajo del árbol principal, cada uno arrancando en profundidad 0.
    /// </summary>
    public static class AITreeAutoLayout
    {
        const float X_SPACING = 280f;
        const float Y_SPACING = 160f;

        public static Dictionary<AIDecisionNode, Vector2> Compute(GraphSnapshot snap)
        {
            var positions = new Dictionary<AIDecisionNode, Vector2>();
            if (snap == null) return positions;

            int siblingCounter = 0;
            if (snap.Root != null) Walk(snap, snap.Root, depth: 0, ref siblingCounter, positions);
            foreach (var d in snap.DetachedRoots())
                Walk(snap, d, depth: 0, ref siblingCounter, positions);
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
    /// Sidecar JSON layout persistence keyed by pre-order traversal index
    /// (<see cref="GraphSnapshot.PreOrder"/>). Stable as long as the tree topology doesn't
    /// change; falls back to auto-layout when keys mismatch.
    /// </summary>
    public static class AITreeLayoutSidecar
    {
        public const string LayoutsDir = "Assets/Rollgeon/Enemies/_layouts";

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

                var ordered = snap.PreOrder();
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
            if (so == null || snap == null || positions == null) return;
            Directory.CreateDirectory(LayoutsDir);

            var ordered = snap.PreOrder();
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

        public static string PathFor(EnemyDataSO so)
        {
            string id = string.IsNullOrEmpty(so.EntityId) ? so.name : so.EntityId;
            return PathForId(id);
        }

        public static string PathForId(string id, string layoutsDir = LayoutsDir)
            => Path.Combine(layoutsDir, $"{id}.json").Replace('\\', '/');
    }
}
