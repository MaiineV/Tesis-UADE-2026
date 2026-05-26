using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    public sealed class AINodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<Type, Vector2> OnSelect; // (subtype, screenMousePos)
        public AIDecisionTreeGraphView GraphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("AI Decision Node"), 0),
            };

            AddCategoryGroup(entries, "Composites", AINodeCategory.Composite);
            AddCategoryGroup(entries, "Branching",  AINodeCategory.Branching);
            AddCategoryGroup(entries, "Leaves",     AINodeCategory.Leaf);

            return entries;
        }

        static void AddCategoryGroup(List<SearchTreeEntry> entries, string label, AINodeCategory cat)
        {
            bool first = true;
            foreach (var meta in AINodeRegistry.All)
            {
                if (meta.Category != cat) continue;
                if (first)
                {
                    entries.Add(new SearchTreeGroupEntry(new GUIContent(label), 1));
                    first = false;
                }
                entries.Add(new SearchTreeEntry(new GUIContent(meta.DisplayName))
                {
                    level = 2,
                    userData = meta.Type,
                });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (!(searchTreeEntry.userData is Type type)) return false;
            OnSelect?.Invoke(type, context.screenMousePosition);
            return true;
        }
    }
}
