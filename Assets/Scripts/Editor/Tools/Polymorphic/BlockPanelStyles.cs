using Rollgeon.Editor.Tools.Polymorphic.Graph;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Section chrome for the authoring panels — colour-coded headers that match the node colours on
    /// the canvas, so a selected block reads the same in both places.
    /// </summary>
    public static class BlockPanelStyles
    {
        /// <summary>Same palette as <see cref="BlockNodeView"/>; the two must not drift.</summary>
        public static Color AccentOf(BlockNodeKind kind)
        {
            switch (kind)
            {
                case BlockNodeKind.Root:      return new Color(0.20f, 0.28f, 0.42f);
                case BlockNodeKind.Hook:      return new Color(0.42f, 0.30f, 0.15f);
                case BlockNodeKind.Group:     return new Color(0.18f, 0.34f, 0.30f);
                case BlockNodeKind.Condition: return new Color(0.40f, 0.34f, 0.12f);
                case BlockNodeKind.Effect:    return new Color(0.24f, 0.24f, 0.42f);
                case BlockNodeKind.Trigger:   return new Color(0.38f, 0.22f, 0.36f);
                case BlockNodeKind.Reader:    return new Color(0.16f, 0.30f, 0.36f);
                default:                      return new Color(0.24f, 0.24f, 0.26f);
            }
        }

        /// <summary>Coloured title bar naming the selected block and its concrete type.</summary>
        public static void DrawNodeHeader(BlockGraphNode node)
        {
            var accent = AccentOf(node.Kind);
            var rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, accent);

            // Brighter left edge — a quick visual key back to the node's colour on the canvas.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), accent * 1.9f);

            var titleRect = new Rect(rect.x + 10f, rect.y + 4f, rect.width - 14f, 18f);
            var subRect = new Rect(rect.x + 10f, rect.y + 21f, rect.width - 14f, 14f);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = Color.white;
            GUI.Label(titleRect, node.Title, titleStyle);

            var subStyle = new GUIStyle(EditorStyles.miniLabel);
            subStyle.normal.textColor = new Color(0.82f, 0.85f, 0.90f);
            GUI.Label(subRect, $"{node.Kind}  ·  {node.Subtitle}", subStyle);

            EditorGUILayout.Space(6);
        }

        /// <summary>
        /// Lists the blocks hanging off this one. They're edited on the canvas, so this only says
        /// what's there and points the author at the right-click menu.
        /// </summary>
        public static void DrawChildrenSummary(BlockGraphNode node)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"Downstream blocks ({node.Children.Count})", EditorStyles.miniBoldLabel);

                foreach (var child in node.Children)
                {
                    var row = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(new Rect(row.x, row.y + 2f, 3f, 12f), AccentOf(child.Kind));
                    GUI.Label(new Rect(row.x + 9f, row.y, row.width - 9f, 16f),
                        $"{child.Title}   ·   {child.Kind}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(
                    "Select one on the canvas to edit it, or right-click this block to add or remove.",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}
