using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    public enum AINodeCategory
    {
        Composite,
        Branching,
        Leaf,
    }

    public readonly struct AINodeMeta
    {
        public readonly Type Type;
        public readonly string DisplayName;
        public readonly AINodeCategory Category;

        public AINodeMeta(Type type, string displayName, AINodeCategory category)
        {
            Type = type;
            DisplayName = displayName;
            Category = category;
        }
    }

    public static class AINodeRegistry
    {
        static List<AINodeMeta> _all;

        public static IReadOnlyList<AINodeMeta> All => _all ??= Build();

        public static AINodeMeta? Find(Type type)
        {
            foreach (var meta in All)
                if (meta.Type == type) return meta;
            return null;
        }

        static List<AINodeMeta> Build()
        {
            var list = new List<AINodeMeta>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<AIDecisionNode>())
            {
                if (t.IsAbstract) continue;
                list.Add(new AINodeMeta(t, DisplayNameFor(t), CategoryFor(t)));
            }
            list.Sort((a, b) =>
            {
                int c = a.Category.CompareTo(b.Category);
                return c != 0 ? c : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });
            return list;
        }

        static string DisplayNameFor(Type t)
        {
            const string prefix = "AINode_";
            string n = t.Name;
            return n.StartsWith(prefix, StringComparison.Ordinal) ? n.Substring(prefix.Length) : n;
        }

        static AINodeCategory CategoryFor(Type t)
        {
            if (typeof(AIActionNode).IsAssignableFrom(t)) return AINodeCategory.Leaf;
            // Branching: nodes that don't expose a uniform Children list — If / Random / While.
            if (t == typeof(AINode_If) || t == typeof(AINode_Random) || t == typeof(AINode_While))
                return AINodeCategory.Branching;
            return AINodeCategory.Composite;
        }
    }
}
