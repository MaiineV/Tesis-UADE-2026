using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ Tab — Raw Data ============================

        // Spec §6.5: this tab used to be a single Context.Tree.Draw(false) call — the whole Odin
        // inspector dumped in one continuous wall. Splitting the asset's top-level fields into
        // collapsible, remembered sections keeps that weight off-screen until the author wants it;
        // a filter jumps straight to a field on bigger assets. (The other big contributor to the
        // weight is [InfoBox] fields that Odin always renders expanded — e.g. PassiveItemHook has
        // four, one five lines long. That's a separate call: see the tool report.)

        Vector2 _dataScroll;
        string _dataFilter = "";

        const string SectionPrefKeyPrefix = "Rollgeon.BlockEditorWindow.RawData.Section.";

        void DrawRawData()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an asset on the left.", MessageType.Info);
                return;
            }

            _ctx.UpdateTree();
            DrawRawDataToolbar();

            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll);
            if (_ctx.Tree != null)
            {
                bool filtering = !string.IsNullOrEmpty(_dataFilter);
                foreach (var section in _ctx.Tree.EnumerateTree(false))
                {
                    if (IsOdinMachinery(section.Name)) continue;
                    if (filtering && !MatchesFilter(section, _dataFilter)) continue;
                    DrawSection(section, filtering);
                }
            }
            EditorGUILayout.EndScrollView();
            _ctx.ApplyChanges();
        }

        void DrawRawDataToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _dataFilter = EditorGUILayout.TextField(_dataFilter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _dataFilter = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Expand all", EditorStyles.toolbarButton, GUILayout.Width(78)))
                SetAllSections(true);
            if (GUILayout.Button("Collapse all", EditorStyles.toolbarButton, GUILayout.Width(84)))
                SetAllSections(false);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void SetAllSections(bool expanded)
        {
            if (_ctx.Tree == null) return;
            foreach (var section in _ctx.Tree.EnumerateTree(false))
            {
                if (IsOdinMachinery(section.Name)) continue;
                EditorPrefs.SetBool(SectionKey(section.Name), expanded);
            }
        }

        void DrawSection(InspectorProperty section, bool forceExpanded)
        {
            var key = SectionKey(section.Name);
            bool expanded = forceExpanded || EditorPrefs.GetBool(key, false);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool next = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, section.NiceName);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!forceExpanded && next != expanded) EditorPrefs.SetBool(key, next);

            if (forceExpanded || next)
            {
                EditorGUILayout.Space(2);
                section.Draw(GUIContent.none);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        // Keyed by the concrete asset type: each BlockEditorWindow<T> subtype owns one fixed field
        // set, so collapse state is shared across every asset the tool edits and survives switching
        // selection — that's the point (spec §6.5, "recordar entre selecciones").
        string SectionKey(string fieldName) => SectionPrefKeyPrefix + typeof(T).Name + "." + fieldName;

        static bool MatchesFilter(InspectorProperty property, string filter)
        {
            if (property.NiceName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (int i = 0; i < property.Children.Count; i++)
                if (MatchesFilter(property.Children[i], filter)) return true;
            return false;
        }

        // Odin surfaces its own plumbing as properties of a SerializedScriptableObject — the
        // serialization blob and the hook it uses to run a type's custom inspector GUI. Neither is
        // content; same exclusion PolymorphicBlockDrawer.ChildrenAt uses for the graph panels.
        static bool IsOdinMachinery(string propertyName) =>
            propertyName == "serializationData" || propertyName == "InternalOnInspectorGUI";
    }
}
