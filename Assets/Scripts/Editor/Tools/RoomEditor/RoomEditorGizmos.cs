using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    internal static class RoomEditorGizmos
    {
        private static readonly Color GridColor = new Color(0.3f, 0.7f, 1f, 0.35f);
        private static readonly Color DoorAssignedColor = new Color(0.4f, 0.95f, 0.45f, 1f);
        private static readonly Color DoorEmptyColor    = new Color(0.65f, 0.65f, 0.65f, 0.75f);

        public static void DrawGridPlane(Vector3 origin, Vector3 step, int layer, int extent)
        {
            float y = origin.y + layer * step.y;
            int half = extent;
            float minX = origin.x + (-half) * step.x;
            float maxX = origin.x + (half + 1) * step.x;
            float minZ = origin.z + (-half) * step.z;
            float maxZ = origin.z + (half + 1) * step.z;

            var prev = Handles.color;
            Handles.color = GridColor;
            for (int i = -half; i <= half + 1; i++)
            {
                float zLine = origin.z + i * step.z;
                Handles.DrawLine(new Vector3(minX, y, zLine), new Vector3(maxX, y, zLine));

                float xLine = origin.x + i * step.x;
                Handles.DrawLine(new Vector3(xLine, y, minZ), new Vector3(xLine, y, maxZ));
            }
            Handles.color = prev;
        }

        public static void DrawCellWire(Vector3 center, Vector3 size, Quaternion rotation, Color color)
        {
            var prevColor = Handles.color;
            var prevMatrix = Handles.matrix;
            Handles.color = color;
            Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, size);
            Handles.matrix = prevMatrix;
            Handles.color = prevColor;
        }

        /// <summary>
        /// Draws an arrow + label per cardinal direction at the perimeter of
        /// <see cref="RoomLayout.LocalBounds"/>. Green = direction already has an assigned door
        /// slot, gray = empty. Used by the Room Editor to hint where doors can be placed.
        /// </summary>
        public static void DrawDoorSlotArrows(RoomLayout layout)
        {
            if (layout == null) return;

            var bounds = layout.LocalBounds;
            var tr = layout.transform;

            DrawDoorArrow(layout, tr, bounds, DoorDirection.North, new Vector3(0f, 0f,  bounds.extents.z));
            DrawDoorArrow(layout, tr, bounds, DoorDirection.South, new Vector3(0f, 0f, -bounds.extents.z));
            DrawDoorArrow(layout, tr, bounds, DoorDirection.East,  new Vector3( bounds.extents.x, 0f, 0f));
            DrawDoorArrow(layout, tr, bounds, DoorDirection.West,  new Vector3(-bounds.extents.x, 0f, 0f));
        }

        private static void DrawDoorArrow(RoomLayout layout, Transform tr, Bounds bounds, DoorDirection dir, Vector3 localOffset)
        {
            var slot = layout.GetDoorSlot(dir);
            bool assigned = slot != null && slot.DoorRoot != null;

            Vector3 worldPos = tr.TransformPoint(bounds.center + localOffset);
            Vector3 outwardLocal = localOffset.sqrMagnitude > 1e-6f ? localOffset.normalized : Vector3.forward;
            Vector3 worldDir = tr.TransformDirection(outwardLocal);

            var prevColor = Handles.color;
            Handles.color = assigned ? DoorAssignedColor : DoorEmptyColor;
            float size = HandleUtility.GetHandleSize(worldPos) * 0.75f;
            Handles.ArrowHandleCap(0, worldPos, Quaternion.LookRotation(worldDir), size, EventType.Repaint);
            Handles.Label(worldPos + Vector3.up * (size * 0.4f), $"{dir}{(assigned ? "" : " (empty)")}");
            Handles.color = prevColor;
        }
    }
}
