using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    internal static class RoomEditorGizmos
    {
        private static readonly Color GridColor = new Color(0.3f, 0.7f, 1f, 0.35f);

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
    }
}
