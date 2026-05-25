using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Scene-view rendering for enemy spawn points: diamond marker + label per SP,
    /// colored by the previewed set (or stacked per set in <c>ColorPerSet</c> mode).
    /// The selected SP picks up a thick white outline.
    /// </summary>
    internal static class RoomEditorSpawnGizmos
    {
        private const float DiamondRadius = 0.35f;
        private const float StackSpacingY = 0.18f;
        private static readonly Color SelectionOutline = Color.white;

        public static void Draw(
            RoomLayout layout,
            int previewSetIndex,
            RoomEditorWindow.SpawnGizmoMode mode,
            Transform selected)
        {
            if (layout == null || mode == RoomEditorWindow.SpawnGizmoMode.Hide) return;
            if (layout.EnemySpawnPoints == null) return;

            foreach (var sp in layout.EnemySpawnPoints)
            {
                if (sp == null) continue;
                var config = sp.GetComponent<SpawnPointConfig>();
                bool isSelected = sp == selected;

                if (mode == RoomEditorWindow.SpawnGizmoMode.PreviewSet)
                {
                    DrawSingle(sp, config, previewSetIndex, isSelected);
                }
                else if (mode == RoomEditorWindow.SpawnGizmoMode.ColorPerSet)
                {
                    DrawStack(sp, config, isSelected);
                }
            }
        }

        private static void DrawSingle(Transform sp, SpawnPointConfig config, int setIndex, bool isSelected)
        {
            var enemy = config != null ? config.GetEnemyForSet(setIndex) : null;
            var color = enemy != null
                ? RoomEditorWindow.ColorForSet(setIndex)
                : new Color(0.55f, 0.55f, 0.55f);

            DrawDiamond(sp.position, color, isSelected);

            string label = enemy != null
                ? $"{sp.name} · {enemy.DisplayName ?? enemy.name}"
                : $"{sp.name} · (set {setIndex} empty)";
            DrawLabel(sp.position, label, color);
        }

        private static void DrawStack(Transform sp, SpawnPointConfig config, bool isSelected)
        {
            int count = config != null ? config.SetCount : 0;
            if (count == 0)
            {
                DrawDiamond(sp.position, new Color(0.55f, 0.55f, 0.55f), isSelected);
                DrawLabel(sp.position, $"{sp.name} · (no sets)", Color.gray);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var pos = sp.position + Vector3.up * (i * StackSpacingY);
                var enemy = config.GetEnemyForSet(i);
                var color = enemy != null ? RoomEditorWindow.ColorForSet(i) : new Color(0.4f, 0.4f, 0.4f);
                DrawDiamond(pos, color, isSelected && i == 0);
            }

            var topPos = sp.position + Vector3.up * (count * StackSpacingY);
            DrawLabel(topPos, $"{sp.name} · {count} set(s)", Color.white);
        }

        private static void DrawDiamond(Vector3 worldPos, Color color, bool isSelected)
        {
            float size = HandleUtility.GetHandleSize(worldPos) * DiamondRadius;

            var prev = Handles.color;

            if (isSelected)
            {
                Handles.color = SelectionOutline;
                Handles.DrawSolidDisc(worldPos, Vector3.up, size * 1.35f);
            }

            Handles.color = color;
            Handles.DrawSolidDisc(worldPos, Vector3.up, size);

            Handles.color = Color.black;
            Handles.DrawWireDisc(worldPos, Vector3.up, size);

            // Vertical pin so it's visible against the ground regardless of camera tilt.
            Handles.color = color;
            Handles.DrawLine(worldPos, worldPos + Vector3.up * size * 1.6f);
            Handles.DrawSolidDisc(worldPos + Vector3.up * size * 1.6f, Vector3.up, size * 0.25f);

            Handles.color = prev;
        }

        private static void DrawLabel(Vector3 worldPos, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter,
            };
            Handles.Label(worldPos + Vector3.up * 0.85f, text, style);
        }
    }
}
