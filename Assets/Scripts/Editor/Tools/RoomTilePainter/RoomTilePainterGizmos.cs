using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomTilePainter
{
    internal static class RoomTilePainterGizmos
    {
        private static readonly Color GridColor = new Color(0.3f, 0.7f, 1f, 0.35f);

        public static void DrawGridPlane(Vector3 origin, Vector3 tileSize, int layer, int extent)
        {
            float y = origin.y + layer * tileSize.y;
            float halfX = tileSize.x * 0.5f;
            float halfZ = tileSize.z * 0.5f;
            int half = extent;

            var prev = Handles.color;
            Handles.color = GridColor;
            for (int i = -half; i <= half; i++)
            {
                Vector3 a = new Vector3(origin.x + (-half) * tileSize.x - halfX, y, origin.z + i * tileSize.z - halfZ);
                Vector3 b = new Vector3(origin.x + ( half) * tileSize.x + halfX, y, origin.z + i * tileSize.z - halfZ);
                Handles.DrawLine(a, b);

                Vector3 c = new Vector3(origin.x + i * tileSize.x - halfX, y, origin.z + (-half) * tileSize.z - halfZ);
                Vector3 d = new Vector3(origin.x + i * tileSize.x - halfX, y, origin.z + ( half) * tileSize.z + halfZ);
                Handles.DrawLine(c, d);
            }
            Handles.color = prev;
        }

        public static void DrawCellWire(Vector3 center, Vector3 size, Color color)
        {
            var prev = Handles.color;
            Handles.color = color;
            Handles.DrawWireCube(center, size);
            Handles.color = prev;
        }
    }
}
