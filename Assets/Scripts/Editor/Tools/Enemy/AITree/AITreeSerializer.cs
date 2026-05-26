using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// In-memory representation the GraphView edits. Each node is a reference to the
    /// actual <see cref="AIDecisionNode"/> instance — non-topological fields (preconditions,
    /// behaviors, params) are still mutated in place so Odin drawers keep working.
    /// </summary>
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
    }

    public static class AITreeSerializer
    {
        // ---- Load ---------------------------------------------------------

        /// <summary>
        /// Walk an existing tree and produce a flat snapshot. Edges are collected in
        /// left-to-right child order so the editor can render them stably.
        /// </summary>
        public static GraphSnapshot Load(AIDecisionNode root)
        {
            var snap = new GraphSnapshot { Root = root };
            if (root == null) return snap;

            var visited = new HashSet<AIDecisionNode>();
            Walk(root, snap, visited);
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

        public sealed class ValidationError
        {
            public readonly AIDecisionNode Node;
            public readonly string Message;
            public ValidationError(AIDecisionNode node, string msg) { Node = node; Message = msg; }
        }

        /// <summary>
        /// Walk the snapshot starting at <see cref="GraphSnapshot.Root"/>, rebuilding child
        /// references on every node so the resulting tree matches the editor topology.
        /// Returns the new root, or null with <paramref name="errors"/> populated.
        /// </summary>
        public static AIDecisionNode Save(GraphSnapshot snap, out List<ValidationError> errors)
        {
            errors = Validate(snap);
            if (errors.Count > 0) return null;

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
                        AITreeTopology.AppendChild(parent, slot, child);
                }
            }

            return snap.Root;
        }

        // ---- Validation ---------------------------------------------------

        static List<ValidationError> Validate(GraphSnapshot snap)
        {
            var errors = new List<ValidationError>();
            if (snap == null) { errors.Add(new ValidationError(null, "Snapshot is null.")); return errors; }
            if (snap.Root == null && snap.Nodes.Count > 0)
                errors.Add(new ValidationError(null, "No root node. Promote one node to root."));

            // Collect inbound edges per node.
            var inbound = new Dictionary<AIDecisionNode, int>();
            foreach (var n in snap.Nodes) inbound[n] = 0;
            foreach (var e in snap.Edges)
                if (inbound.ContainsKey(e.Child)) inbound[e.Child]++;

            // Orphans (other than root).
            foreach (var n in snap.Nodes)
            {
                if (n == snap.Root) continue;
                if (!inbound.TryGetValue(n, out int c) || c == 0)
                    errors.Add(new ValidationError(n, "Orphan node — connect it or remove it."));
                if (c > 1)
                    errors.Add(new ValidationError(n, "Node has multiple parents — tree must be acyclic and unique-parent."));
            }

            // If-nodes need both Then and Else (Else null is legal at runtime, but the editor
            // surfaces it as a missing connection so authors don't lose branches by accident).
            foreach (var n in snap.Nodes)
            {
                if (n is AINode_If)
                {
                    bool hasThen = false;
                    foreach (var e in snap.Edges)
                    {
                        if (e.Parent == n && e.SlotIndex == 0) { hasThen = true; break; }
                    }
                    if (!hasThen)
                        errors.Add(new ValidationError(n, "If-node has no `Then` branch connected."));
                }
            }

            // Cycle detection via DFS.
            if (snap.Root != null)
            {
                var stack = new Stack<AIDecisionNode>();
                var path = new HashSet<AIDecisionNode>();
                if (HasCycle(snap.Root, snap, stack, path))
                    errors.Add(new ValidationError(null, "Tree contains a cycle."));
            }

            return errors;
        }

        static bool HasCycle(AIDecisionNode node, GraphSnapshot snap, Stack<AIDecisionNode> stack, HashSet<AIDecisionNode> path)
        {
            if (!path.Add(node)) return true;
            stack.Push(node);
            foreach (var e in snap.Edges)
            {
                if (e.Parent != node) continue;
                if (HasCycle(e.Child, snap, stack, path)) return true;
            }
            stack.Pop();
            path.Remove(node);
            return false;
        }
    }
}
