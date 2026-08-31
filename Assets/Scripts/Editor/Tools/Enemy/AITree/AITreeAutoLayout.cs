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
    /// Sidecar JSON layout persistence keyed by the stable per-node
    /// <see cref="AIDecisionNode.EditorNodeId"/> (assigned lazily on save), with a legacy
    /// fallback to the pre-order traversal index (<see cref="GraphSnapshot.PreOrder"/>) for
    /// files written before ids existed — or when a node hasn't persisted its id yet.
    /// Falls back to auto-layout when nothing matches.
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
                public string Id;      // EditorNodeId — vacío en archivos legacy
                public int Index;      // fallback legacy + debug
                public string TypeName;
                public Vector2 Position;
            }
        }

        public static Dictionary<AIDecisionNode, Vector2> Load(
            EnemyDataSO so, GraphSnapshot snap, string layoutsDir = LayoutsDir)
        {
            string path = PathFor(so, layoutsDir);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<LayoutFile>(json);
                if (data?.Entries == null) return null;

                var ordered = snap.PreOrder();
                var byId = new Dictionary<string, AIDecisionNode>();
                foreach (var n in ordered)
                {
                    if (n == null || string.IsNullOrEmpty(n.EditorNodeId)) continue;
                    if (!byId.ContainsKey(n.EditorNodeId)) byId[n.EditorNodeId] = n;
                }

                var result = new Dictionary<AIDecisionNode, Vector2>();
                foreach (var entry in data.Entries)
                {
                    // Primero por id estable: sobrevive inserts de un nodo del MISMO tipo
                    // (el índice de preorden corría todo y aplicaba posiciones al nodo
                    // equivocado en silencio). Miss de id o entrada legacy → el camino
                    // histórico por índice + guard de tipo.
                    if (!string.IsNullOrEmpty(entry.Id) && byId.TryGetValue(entry.Id, out var idNode))
                    {
                        result[idNode] = entry.Position;
                        continue;
                    }

                    if (entry.Index < 0 || entry.Index >= ordered.Count) continue;
                    var node = ordered[entry.Index];
                    if (node == null) continue;
                    if (node.GetType().Name != entry.TypeName) continue; // topology drift
                    if (result.ContainsKey(node)) continue;
                    result[node] = entry.Position;
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(EnemyDataSO so, GraphSnapshot snap,
            Dictionary<AIDecisionNode, Vector2> positions, string layoutsDir = LayoutsDir)
        {
            if (so == null || snap == null || positions == null) return;
            Directory.CreateDirectory(layoutsDir);

            var ordered = snap.PreOrder();
            var data = new LayoutFile();
            bool assignedAnyId = false;
            for (int i = 0; i < ordered.Count; i++)
            {
                var n = ordered[i];
                if (n == null || !positions.TryGetValue(n, out var pos)) continue;
                if (string.IsNullOrEmpty(n.EditorNodeId))
                {
                    // Asignación lazy: los árboles viejos migran al formato por id en su
                    // primer guardado.
                    n.EditorNodeId = System.Guid.NewGuid().ToString("N");
                    assignedAnyId = true;
                }
                data.Entries.Add(new LayoutFile.Entry
                {
                    Id = n.EditorNodeId,
                    Index = i,
                    TypeName = n.GetType().Name,
                    Position = pos,
                });
            }

            // Los ids viven en el blob Odin del SO: si asignamos alguno, el asset tiene que
            // quedar dirty para que persista — si no, al reload los nodos vuelven sin id y
            // las entradas del JSON caen al fallback por índice (comportamiento viejo).
            if (assignedAnyId && !UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorUtility.SetDirty(so);

            // Sidecar lives on disk only; we never read it via AssetDatabase, so skip
            // the Refresh — calling it in the middle of an edit caused field values to
            // round-trip through Unity's importer and revert in-memory edits.
            File.WriteAllText(PathFor(so, layoutsDir), JsonUtility.ToJson(data, prettyPrint: true));
        }

        public static string PathFor(EnemyDataSO so, string layoutsDir = LayoutsDir)
        {
            string id = string.IsNullOrEmpty(so.EntityId) ? so.name : so.EntityId;
            return PathForId(id, layoutsDir);
        }

        public static string PathForId(string id, string layoutsDir = LayoutsDir)
            => Path.Combine(layoutsDir, $"{id}.json").Replace('\\', '/');
    }
}
