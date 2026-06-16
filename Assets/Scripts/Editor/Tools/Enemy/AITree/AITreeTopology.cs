using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Per-subtype knowledge of how an <see cref="AIDecisionNode"/> connects to its children.
    /// Encapsulated here so the GraphView/serializer/auto-layout don't repeat type checks.
    /// </summary>
    public static class AITreeTopology
    {
        public readonly struct Slot
        {
            public readonly string Name;     // shown on the port label
            public readonly bool IsDynamic;  // true → user can add more children of this slot kind
            public Slot(string name, bool isDynamic) { Name = name; IsDynamic = isDynamic; }
        }

        public static IReadOnlyList<Slot> SlotsOf(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Selector _:  return _dynamicChildren;
                case AINode_Sequence _:  return _dynamicChildren;
                case AINode_If _:        return _ifSlots;
                case AINode_Random _:    return _randomOptions;
                case AINode_While _:     return _whileSlots;
                case AINode_Once _:      return _onceSlot;
                default:                 return Array.Empty<Slot>(); // leaves
            }
        }

        public static IReadOnlyList<AIDecisionNode> ChildrenOf(AIDecisionNode node, out IReadOnlyList<int> slotIndices)
        {
            var children = new List<AIDecisionNode>();
            var slots = new List<int>();
            switch (node)
            {
                case AINode_Selector s:
                    if (s.Children != null) foreach (var c in s.Children) { children.Add(c); slots.Add(0); }
                    break;
                case AINode_Sequence s:
                    if (s.Children != null) foreach (var c in s.Children) { children.Add(c); slots.Add(0); }
                    break;
                case AINode_If i:
                    children.Add(i.Then); slots.Add(0);
                    children.Add(i.Else); slots.Add(1);
                    break;
                case AINode_Random r:
                    if (r.Options != null) foreach (var o in r.Options) { children.Add(o.Node); slots.Add(0); }
                    break;
                case AINode_While w:
                    children.Add(w.Body); slots.Add(0);
                    break;
                case AINode_Once o:
                    children.Add(o.Child); slots.Add(0);
                    break;
            }
            slotIndices = slots;
            return children;
        }

        public static void ClearChildren(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Selector s: s.Children = new List<AIDecisionNode>(); break;
                case AINode_Sequence s: s.Children = new List<AIDecisionNode>(); break;
                case AINode_If i: i.Then = null; i.Else = null; break;
                case AINode_Random r: r.Options = new List<AINode_Random.Option>(); break;
                case AINode_While w: w.Body = null; break;
                case AINode_Once o: o.Child = null; break;
            }
        }

        /// <summary>
        /// Append <paramref name="child"/> into <paramref name="parent"/> at <paramref name="slotIndex"/>.
        /// For dynamic-children slots (Selector/Sequence/Random), <paramref name="slotIndex"/> is always 0
        /// and order is determined by call order — caller must invoke in left-to-right order.
        /// </summary>
        public static void AppendChild(AIDecisionNode parent, int slotIndex, AIDecisionNode child)
        {
            switch (parent)
            {
                case AINode_Selector s: s.Children.Add(child); break;
                case AINode_Sequence s: s.Children.Add(child); break;
                case AINode_If i:
                    if (slotIndex == 0) i.Then = child;
                    else i.Else = child;
                    break;
                case AINode_Random r:
                    r.Options.Add(new AINode_Random.Option { Node = child, Weight = 1f });
                    break;
                case AINode_While w:
                    w.Body = child;
                    break;
                case AINode_Once o:
                    o.Child = child;
                    break;
            }
        }

        /// <summary>True if the node accepts at least one outgoing connection.</summary>
        public static bool CanHaveChildren(AIDecisionNode node) => SlotsOf(node).Count > 0;

        // ---- canonical slot configurations -------------------------------

        static readonly Slot[] _dynamicChildren = { new Slot("Children", true) };
        static readonly Slot[] _ifSlots = { new Slot("Then", false), new Slot("Else", false) };
        static readonly Slot[] _randomOptions = { new Slot("Options", true) };
        static readonly Slot[] _whileSlots = { new Slot("Body", false) };
        static readonly Slot[] _onceSlot = { new Slot("Child", false) };
    }
}
