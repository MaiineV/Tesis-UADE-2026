using Rollgeon.Editor.Tools.Polymorphic.Graph;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ Side panel — selected node ============================

        Vector2 _panelScroll;

        /// <summary>Optional per-asset warnings drawn above the graph.</summary>
        protected virtual void DrawIssues(T asset) { }

        void DrawSidePanel()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an asset on the left.", MessageType.Info);
                return;
            }

            _ctx.UpdateTree();
            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);

            DrawIssues(_selected);

            if (_selectedNode == null)
            {
                EditorGUILayout.HelpBox("Select a block in the graph to edit it.", MessageType.Info);
            }
            else if (_selectedNode.Kind == BlockNodeKind.Root)
            {
                // The asset's own fields — id, display name, icon, cooldown… Everything except the
                // blocks, which are nodes to the right. Saves a trip to the Raw Data tab for the
                // fields an author touches most.
                BlockPanelStyles.DrawNodeHeader(_selectedNode);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PolymorphicBlockDrawer.DrawNode(
                        _ctx, _selected, string.Empty, PolymorphicBlockDrawer.Options.Default);
                    DrawRenameButton();
                }

                EditorGUILayout.Space(4);
                DrawCatalogButton();

                if (_selectedNode.Children.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    BlockPanelStyles.DrawChildrenSummary(_selectedNode);
                }
            }
            else
            {
                BlockPanelStyles.DrawNodeHeader(_selectedNode);

                // The cached path goes stale whenever a list shifts — re-resolve against the live
                // object before drawing, or the panel would edit the wrong element.
                if (!_ctx.PathPointsTo(_selectedNode.Path, _selectedNode.Value))
                {
                    var repaired = _ctx.FindPathTo(_selectedNode.Value);
                    if (string.IsNullOrEmpty(repaired))
                    {
                        EditorGUILayout.HelpBox(
                            "This block is no longer reachable — it was probably removed. " +
                            "Select another one.", MessageType.Warning);
                        EditorGUILayout.EndScrollView();
                        return;
                    }
                    _selectedNode.Path = repaired;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PolymorphicBlockDrawer.DrawNode(
                        _ctx, _selectedNode.Value, _selectedNode.Path, PolymorphicBlockDrawer.Options.Default);
                }

                if (_selectedNode.Children.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    BlockPanelStyles.DrawChildrenSummary(_selectedNode);
                }
            }

            EditorGUILayout.EndScrollView();
            _ctx.ApplyChanges();
        }
    }
}
