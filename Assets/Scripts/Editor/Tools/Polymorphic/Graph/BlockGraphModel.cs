using System;
using System.Collections;
using System.Collections.Generic;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>Coarse node category. Drives colour and icon only — never behaviour.</summary>
    public enum BlockNodeKind
    {
        Root,
        Hook,       // PassiveItemHook, behavior — something that says *when*
        Group,      // EffectData — a precondition set + an effect chain
        Condition,  // BasePreCondition
        Effect,     // IEffect
        Trigger,    // IEnchantmentTrigger / IComboPassiveTrigger
        Reader,     // EffectIntReader
        Container,  // ChainPhase and other plumbing
    }

    /// <summary>
    /// One box on the canvas. <see cref="Path"/> is the identity: the graph is a projection of
    /// list order and containment, so a node is only ever "the thing living at this Odin path".
    /// </summary>
    public sealed class BlockGraphNode
    {
        public string Path;
        public string Title;
        public string Subtitle;
        public BlockNodeKind Kind;
        public object Value;
        public int Column;
        public readonly List<BlockGraphNode> Children = new List<BlockGraphNode>();
    }

    /// <summary>
    /// Projects an Odin-serialized asset into a tree of authoring blocks, left to right:
    /// asset → hooks → effect groups → conditions and effects → whatever those nest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here is authored topology.</b> Every parent/child edge is derived from a field or
    /// a list index, so the model is rebuilt from scratch after each edit and node positions are
    /// never persisted — a saved position could drift out of sync with list order, and then the
    /// canvas would be lying about execution order.
    /// </para>
    /// <para>
    /// The same tree feeds the panel (drawn indented) and the graph (drawn left→right). Two views,
    /// one model.
    /// </para>
    /// </remarks>
    public static class BlockGraphModel
    {
        public const int MAX_DEPTH = 8;

        public sealed class Result
        {
            public BlockGraphNode Root;
            public readonly List<BlockGraphNode> AllNodes = new List<BlockGraphNode>();
        }

        public static Result Build(UnityEngine.Object asset)
        {
            var result = new Result();
            if (asset == null) return result;

            result.Root = new BlockGraphNode
            {
                Path = string.Empty,
                Title = asset.name,
                Subtitle = asset.GetType().Name,
                Kind = BlockNodeKind.Root,
                Value = asset,
                Column = 0,
            };
            result.AllNodes.Add(result.Root);

            Walk(asset, result.Root, string.Empty, 0, result, new HashSet<object>(ReferenceComparer.Instance));
            return result;
        }

        static void Walk(object value, BlockGraphNode parent, string path, int depth,
                         Result result, HashSet<object> visited)
        {
            if (value == null || depth >= MAX_DEPTH) return;
            // Guard shared references: the same instance reachable twice would recurse forever.
            if (!value.GetType().IsValueType && !visited.Add(value)) return;

            var type = value.GetType();

            // Containers first — they're the structural spine (hooks, groups, chain phases).
            foreach (var block in PolymorphicMemberScanner.BlockMembersOf(type))
                Emit(block, value, parent, path, depth, result, visited);

            // Then the slots whose concrete type the designer picked.
            foreach (var picker in PolymorphicMemberScanner.Scan(type))
                Emit(picker, value, parent, path, depth, result, visited);
        }

        static void Emit(PolymorphicMember member, object owner, BlockGraphNode parent,
                         string ownerPath, int depth, Result result, HashSet<object> visited)
        {
            var raw = member.Field.GetValue(owner);
            if (raw == null) return;

            string memberPath = Join(ownerPath, member.Name);

            if (!member.IsList)
            {
                AddNode(raw, parent, memberPath, member, -1, depth, result, visited);
                return;
            }

            if (!(raw is IList list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                AddNode(list[i], parent, memberPath + ".$" + i, member, i, depth, result, visited);
            }
        }

        static void AddNode(object value, BlockGraphNode parent, string path, PolymorphicMember member,
                            int index, int depth, Result result, HashSet<object> visited)
        {
            var node = new BlockGraphNode
            {
                Path = path,
                Title = TitleFor(value, member, index),
                Subtitle = value.GetType().Name,
                Kind = KindFor(value, member),
                Value = value,
                Column = parent.Column + 1,
            };
            parent.Children.Add(node);
            result.AllNodes.Add(node);
            Walk(value, node, path, depth + 1, result, visited);
        }

        static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "." + b;

        /// <summary>Presentation only — a designer's label beats a class name where one exists.</summary>
        static string TitleFor(object value, PolymorphicMember member, int index)
        {
            if (value is ChainPhase) return index >= 0 ? $"Phase {index + 1}" : "Phase";

            if (value is EffectData group)
                return string.IsNullOrEmpty(group.Label) ? "Effect Group" : group.Label;

            string authored = AuthoredName(value);
            return string.IsNullOrEmpty(authored) ? value.GetType().Name : authored;
        }

        /// <summary>
        /// The name a designer gave this block, if any. Probed by convention rather than by type so
        /// new effect/trigger families title themselves without touching this file.
        /// </summary>
        static string AuthoredName(object value)
        {
            var type = value.GetType();
            foreach (var memberName in NameMembers)
            {
                var prop = type.GetProperty(memberName, Flags);
                if (prop != null && prop.PropertyType == typeof(string) && prop.CanRead)
                {
                    var s = prop.GetValue(value) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                var field = type.GetField(memberName, Flags);
                if (field == null) continue;
                if (field.FieldType == typeof(string))
                {
                    var s = field.GetValue(value) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                else if (field.FieldType.IsEnum)
                {
                    return field.GetValue(value).ToString();
                }
            }
            return null;
        }

        static BlockNodeKind KindFor(object value, PolymorphicMember member)
        {
            if (value is EffectData) return BlockNodeKind.Group;
            if (value is BasePreCondition) return BlockNodeKind.Condition;
            if (value is IEffect) return BlockNodeKind.Effect;
            if (value is EffectIntReader) return BlockNodeKind.Reader;
            if (value is ChainPhase) return BlockNodeKind.Container;

            // Trigger and hook families (enchantments, combo passives, item hooks, behaviors) share
            // no base type — they're parallel hierarchies. Match on the declared base's name so a
            // new family classifies itself instead of needing a case added here.
            string baseName = member.BaseType.Name;
            if (baseName.IndexOf("Trigger", StringComparison.Ordinal) >= 0) return BlockNodeKind.Trigger;
            if (baseName.IndexOf("Hook", StringComparison.Ordinal) >= 0) return BlockNodeKind.Hook;
            if (baseName.EndsWith("Behavior", StringComparison.Ordinal)) return BlockNodeKind.Hook;

            return BlockNodeKind.Container;
        }

        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        static readonly string[] NameMembers =
        {
            "Label", "ActionName", "DisplayName", "ConditionName", "TriggerEvent", "Trigger", "Event",
        };

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
