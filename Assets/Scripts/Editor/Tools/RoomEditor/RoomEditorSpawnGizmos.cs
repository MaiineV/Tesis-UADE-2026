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
                    DrawSingle(layout, sp, config, previewSetIndex, isSelected);
                }
                else if (mode == RoomEditorWindow.SpawnGizmoMode.ColorPerSet)
                {
                    DrawStack(layout, sp, config, isSelected);
                }
            }
        }

        private static void DrawSingle(RoomLayout layout, Transform sp, SpawnPointConfig config,
            int setIndex, bool isSelected)
        {
            var enemy = config != null ? config.GetEnemyForSet(setIndex) : null;
            var color = enemy != null
                ? RoomEditorWindow.ColorForSet(setIndex)
                : new Color(0.55f, 0.55f, 0.55f);

            // Hollow marker: this spawn point spawns nothing for the previewed set.
            DrawDiamond(sp.position, color, isSelected, hollow: enemy == null);
            DrawFootprint(layout, sp, enemy, color);

            string label = enemy != null
                ? $"{sp.name} · {enemy.DisplayName ?? enemy.name}{FootprintSuffix(enemy)}"
                : $"{sp.name} · (set {setIndex}: nothing spawns)";
            DrawLabel(sp.position, label, color);
        }

        private static void DrawStack(RoomLayout layout, Transform sp, SpawnPointConfig config, bool isSelected)
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
                DrawDiamond(pos, color, isSelected && i == 0, hollow: enemy == null);
                DrawFootprint(layout, sp, enemy, color);
            }

            var topPos = sp.position + Vector3.up * (count * StackSpacingY);
            DrawLabel(topPos, $"{sp.name} · {count} set(s)", Color.white);
        }

        private static string FootprintSuffix(Rollgeon.Entities.EnemyDataSO enemy)
        {
            if (enemy == null || !enemy.HasMultiCellFootprint) return string.Empty;
            var fp = enemy.EffectiveFootprint;
            return $" · {fp.x}×{fp.y}";
        }

        /// <summary>
        /// Rectángulo del footprint del enemigo asignado: min-corner = la celda del SP,
        /// tamaño = footprint × TileSize (misma semántica que los blockers de
        /// <see cref="RoomLayout.OnDrawGizmosSelected"/>). Es la posición DESEADA — en
        /// runtime el ancla puede correrse hasta radio 3 si el rect no cabe.
        /// </summary>
        private static void DrawFootprint(RoomLayout layout, Transform sp,
            Rollgeon.Entities.EnemyDataSO enemy, Color color)
        {
            if (layout == null || enemy == null || !enemy.HasMultiCellFootprint) return;

            float ts = layout.TileSize;
            if (ts <= 0f) ts = 1f;
            var origin = layout.GetOrigin();
            int cx = Mathf.FloorToInt((sp.position.x - origin.x) / ts);
            int cy = Mathf.FloorToInt((sp.position.z - origin.z) / ts);
            var fp = enemy.EffectiveFootprint;
            float sx = fp.x * ts;
            float sz = fp.y * ts;
            var center = new Vector3(
                origin.x + cx * ts + sx * 0.5f,
                sp.position.y,
                origin.z + cy * ts + sz * 0.5f);

            var prev = Handles.color;
            Handles.color = color;
            Handles.DrawWireCube(center, new Vector3(sx, 0.02f, sz));
            Handles.color = prev;
        }

        private static void DrawDiamond(Vector3 worldPos, Color color, bool isSelected, bool hollow = false)
        {
            float size = HandleUtility.GetHandleSize(worldPos) * DiamondRadius;

            var prev = Handles.color;

            if (isSelected)
            {
                Handles.color = SelectionOutline;
                Handles.DrawSolidDisc(worldPos, Vector3.up, size * 1.35f);
            }

            Handles.color = color;
            if (hollow)
            {
                Handles.DrawWireDisc(worldPos, Vector3.up, size);
                Handles.DrawWireDisc(worldPos, Vector3.up, size * 0.6f);
            }
            else
            {
                Handles.DrawSolidDisc(worldPos, Vector3.up, size);
            }

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
