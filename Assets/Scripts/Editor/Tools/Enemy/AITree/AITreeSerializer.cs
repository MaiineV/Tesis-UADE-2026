using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// In-memory representation the GraphView edits. Each node is a reference to the
    /// actual <see cref="AIDecisionNode"/> instance — non-topological fields (preconditions,
    /// behaviors, params) are still mutated in place so Odin drawers keep working.
    /// </summary>
    /// <remarks>
    /// Un nodo sin edge entrante y distinto de <see cref="Root"/> es la raíz de un subárbol
    /// "suelto": persiste en <see cref="EnemyDataSO.AIDetachedNodes"/> y el runtime lo ignora.
    /// <see cref="PreOrder"/> es la única regla de orden estable (raíz, luego cada suelto): la
    /// usan el sidecar de layout y el auto-layout.
    /// </remarks>
    public sealed class GraphSnapshot
    {
        public readonly List<AIDecisionNode> Nodes = new List<AIDecisionNode>();
        public readonly List<Edge> Edges = new List<Edge>();
        public AIDecisionNode Root;

        public readonly struct Edge
        {
            public readonly AIDecisionNode Parent;
            public readonly int SlotIndex;
            public readonly AIDecisionNode Child;
            public Edge(AIDecisionNode parent, int slotIndex, AIDecisionNode child)
            {
                Parent = parent; SlotIndex = slotIndex; Child = child;
            }
        }

        /// <summary>Hijos de <paramref name="parent"/> en <paramref name="slot"/>, en orden de ejecución (= orden de edges).</summary>
        public List<AIDecisionNode> ChildrenOf(AIDecisionNode parent, int slot)
        {
            var list = new List<AIDecisionNode>();
            foreach (var e in Edges)
                if (e.Parent == parent && e.SlotIndex == slot) list.Add(e.Child);
            return list;
        }

        /// <summary>Padre y slot del que cuelga <paramref name="child"/>; <c>false</c> si es raíz o suelto.</summary>
        public bool TryGetParent(AIDecisionNode child, out AIDecisionNode parent, out int slot)
        {
            foreach (var e in Edges)
            {
                if (e.Child != child) continue;
                parent = e.Parent; slot = e.SlotIndex;
                return true;
            }
            parent = null; slot = -1;
            return false;
        }

        /// <summary>Raíces de los subárboles sueltos: sin edge entrante y distintas de <see cref="Root"/>, en orden de <see cref="Nodes"/>.</summary>
        public List<AIDecisionNode> DetachedRoots()
        {
            var inbound = new HashSet<AIDecisionNode>();
            foreach (var e in Edges) inbound.Add(e.Child);
            var list = new List<AIDecisionNode>();
            foreach (var n in Nodes)
                if (n != null && n != Root && !inbound.Contains(n)) list.Add(n);
            return list;
        }

        /// <summary>Recorrido en preorden: raíz y luego cada subárbol suelto. Índice estable para el sidecar.</summary>
        public List<AIDecisionNode> PreOrder()
        {
            var list = new List<AIDecisionNode>();
            var visited = new HashSet<AIDecisionNode>();
            Visit(Root, visited, list);
            foreach (var d in DetachedRoots()) Visit(d, visited, list);
            return list;
        }

        void Visit(AIDecisionNode node, HashSet<AIDecisionNode> visited, List<AIDecisionNode> list)
        {
            if (node == null || !visited.Add(node)) return;
            list.Add(node);
            foreach (var e in Edges)
                if (e.Parent == node) Visit(e.Child, visited, list);
        }

        /// <summary><c>true</c> si <paramref name="ancestor"/> está por encima de <paramref name="node"/> (estricto: un nodo no es ancestro de sí mismo).</summary>
        public bool IsAncestor(AIDecisionNode ancestor, AIDecisionNode node)
        {
            if (ancestor == null || node == null || ancestor == node) return false;
            var visited = new HashSet<AIDecisionNode>();
            var queue = new Queue<AIDecisionNode>();
            queue.Enqueue(node);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!TryGetParent(current, out var parent, out _)) continue;
                if (parent == ancestor) return true;
                if (visited.Add(parent)) queue.Enqueue(parent);
            }
            return false;
        }

        /// <summary>
        /// Mueve el hijo <paramref name="fromIndex"/> de (<paramref name="parent"/>, <paramref name="slot"/>)
        /// a la posición <paramref name="toIndex"/> entre sus hermanos. Solo se reordenan los edges de
        /// ese slot, en sus mismas posiciones de la lista global.
        /// </summary>
        public bool MoveChild(AIDecisionNode parent, int slot, int fromIndex, int toIndex)
        {
            var positions = new List<int>();
            for (int i = 0; i < Edges.Count; i++)
                if (Edges[i].Parent == parent && Edges[i].SlotIndex == slot) positions.Add(i);

            if (fromIndex < 0 || fromIndex >= positions.Count) return false;
            if (toIndex < 0 || toIndex >= positions.Count) return false;
            if (fromIndex == toIndex) return false;

            var siblings = new List<Edge>(positions.Count);
            foreach (var p in positions) siblings.Add(Edges[p]);
            var moved = siblings[fromIndex];
            siblings.RemoveAt(fromIndex);
            siblings.Insert(toIndex, moved);
            for (int i = 0; i < positions.Count; i++) Edges[positions[i]] = siblings[i];
            return true;
        }
    }

    public static class AITreeSerializer
    {
        // ---- Load ---------------------------------------------------------

        /// <summary>
        /// Walk an existing tree and produce a flat snapshot. Edges are collected in
        /// left-to-right child order so the editor can render them stably. Los subárboles
        /// sueltos (<paramref name="detached"/>) se recorren después de la raíz.
        /// </summary>
        public static GraphSnapshot Load(AIDecisionNode root, IReadOnlyList<AIDecisionNode> detached = null)
        {
            var snap = new GraphSnapshot { Root = root };
            var visited = new HashSet<AIDecisionNode>();
            if (root != null) Walk(root, snap, visited);
            if (detached != null)
            {
                foreach (var d in detached) Walk(d, snap, visited);
            }
            return snap;
        }

        static void Walk(AIDecisionNode node, GraphSnapshot snap, HashSet<AIDecisionNode> visited)
        {
            if (node == null || !visited.Add(node)) return;
            snap.Nodes.Add(node);

            var children = AITreeTopology.ChildrenOf(node, out var slots);
            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                if (c == null) continue;
                snap.Edges.Add(new GraphSnapshot.Edge(node, slots[i], c));
                Walk(c, snap, visited);
            }
        }

        // ---- Save ---------------------------------------------------------

        /// <summary>Overload sin sueltos: los subárboles desconectados se descartan del resultado.</summary>
        public static AIDecisionNode Save(GraphSnapshot snap, out List<ValidationIssue> issues)
            => Save(snap, out _, out issues);

        /// <summary>
        /// Walk the snapshot starting at <see cref="GraphSnapshot.Root"/>, rebuilding child
        /// references on every node so the resulting tree matches the editor topology.
        /// Returns the new root (null si el snapshot no tiene raíz o si hay un
        /// <see cref="IssueSeverity.Error"/>); <paramref name="detachedRoots"/> recibe las raíces
        /// de los subárboles sueltos, ya reconstruidos.
        /// </summary>
        public static AIDecisionNode Save(GraphSnapshot snap, out List<AIDecisionNode> detachedRoots,
                                          out List<ValidationIssue> issues)
        {
            detachedRoots = new List<AIDecisionNode>();
            issues = AITreeValidator.Validate(snap);
            if (AITreeValidator.HasErrors(issues)) return null;

            // Los pesos de Random viven en el nodo y ClearChildren los destruye: capturarlos
            // antes, por (parent, child), para que sigan al hijo aunque cambie el orden de edges.
            var weights = new Dictionary<(AIDecisionNode, AIDecisionNode), float>();
            foreach (var n in snap.Nodes) AITreeTopology.CaptureEdgeWeights(n, weights);

            // Group edges: parent → slotIndex → ordered children.
            var byParent = new Dictionary<AIDecisionNode, SortedDictionary<int, List<AIDecisionNode>>>();
            foreach (var e in snap.Edges)
            {
                if (!byParent.TryGetValue(e.Parent, out var slots))
                {
                    slots = new SortedDictionary<int, List<AIDecisionNode>>();
                    byParent[e.Parent] = slots;
                }
                if (!slots.TryGetValue(e.SlotIndex, out var children))
                {
                    children = new List<AIDecisionNode>();
                    slots[e.SlotIndex] = children;
                }
                children.Add(e.Child);
            }

            foreach (var n in snap.Nodes) AITreeTopology.ClearChildren(n);

            foreach (var kv in byParent)
            {
                var parent = kv.Key;
                foreach (var slotPair in kv.Value)
                {
                    int slot = slotPair.Key;
                    foreach (var child in slotPair.Value)
                    {
                        float weight = weights.TryGetValue((parent, child), out var w) ? w : 1f;
                        AITreeTopology.AppendChild(parent, slot, child, weight);
                    }
                }
            }

            detachedRoots = snap.DetachedRoots();
            return snap.Root;
        }

        /// <summary>
        /// Valida, registra undo y escribe árbol + sueltos en <paramref name="enemy"/>, en ese
        /// orden. Solo un <see cref="IssueSeverity.Error"/> impide escribir; avisos e info se
        /// devuelven igual para que el canvas los muestre.
        /// </summary>
        /// <remarks>
        /// El orden importa: <see cref="Save(GraphSnapshot, out List{AIDecisionNode}, out List{ValidationIssue})"/>
        /// muta los nodos in-place, así que <c>Undo.RecordObject</c> tiene que correr ANTES — el
        /// snapshot de undo serializa el SO en ese instante (regenera el blob Odin desde el estado
        /// vivo). Grabar después deja un "antes" idéntico al "después" y Ctrl+Z no revierte nada.
        /// </remarks>
        public static bool Commit(EnemyDataSO enemy, GraphSnapshot snap, string undoLabel,
                                  out List<ValidationIssue> issues)
        {
            issues = AITreeValidator.Validate(snap);
            if (enemy == null)
            {
                issues.Add(new ValidationIssue(null, "Ningún enemigo seleccionado.", IssueSeverity.Error));
                return false;
            }
            if (AITreeValidator.HasErrors(issues)) return false;

            UnityEditor.Undo.RecordObject(enemy, undoLabel);
            enemy.AIRoot = Save(snap, out var detached, out _);
            enemy.AIDetachedNodes = detached;
            UnityEditor.EditorUtility.SetDirty(enemy);
            return true;
        }
    }
}
