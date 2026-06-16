using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Pure editor operations to seed <see cref="WallOccluder"/> components on
    /// wall tiles painted by the Room Editor (§17.E.8). Shared between the
    /// paint-time hook (<c>RoomEditorWindow.PlaceAt</c>) and the
    /// <c>Bake Wall Occluders</c> button so both keep identical semantics.
    /// </summary>
    public static class WallOccluderOps
    {
        public const string UndoLabel = "Bake Wall Occluders";

        public enum BakeResult { Added, Updated, Skipped }

        /// <summary>
        /// Ensures <paramref name="tile"/> has a <see cref="WallOccluder"/> and
        /// that its <see cref="WallOccluder.Direction"/> matches the cell's
        /// position relative to the room. Respects
        /// <see cref="WallOccluder.ManualOverride"/>.
        /// </summary>
        public static BakeResult EnsureOccluder(GameObject tile, RoomLayout room, Vector3Int cell)
        {
            if (tile == null || room == null) return BakeResult.Skipped;

            var centerCell = ComputeRoomCenterCell(room);
            var direction = InferDirection(cell, centerCell);

            var occluder = tile.GetComponent<WallOccluder>();
            if (occluder == null)
            {
                occluder = Undo.AddComponent<WallOccluder>(tile);
                occluder.Direction = direction;
                EditorUtility.SetDirty(occluder);
                return BakeResult.Added;
            }

            if (occluder.ManualOverride) return BakeResult.Skipped;
            if (occluder.Direction == direction) return BakeResult.Skipped;

            Undo.RecordObject(occluder, UndoLabel);
            occluder.Direction = direction;
            EditorUtility.SetDirty(occluder);
            return BakeResult.Updated;
        }

        /// <summary>
        /// Quantizes the vector from <paramref name="centerCell"/> to
        /// <paramref name="cell"/> into one of 8 compass octants. Operates in
        /// cell space — invariant under <see cref="RoomLayout.GridOrigin"/>.
        /// Unity convention: +Z = N, +X = E.
        /// </summary>
        public static WallDirection InferDirection(Vector3Int cell, Vector3 centerCell)
        {
            float dx = cell.x - centerCell.x;
            float dz = cell.z - centerCell.z;

            // Same cell as the center → arbitrary but deterministic default.
            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dz, 0f))
                return WallDirection.N;

            // atan2(x, z) puts compass N (+Z) at 0° and rotates clockwise:
            // E=90°, S=180°, W=270°. Matches the WallDirection enum order.
            float angle = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            int octant = Mathf.RoundToInt(angle / 45f) % 8;
            return (WallDirection)octant;
        }

        /// <summary>
        /// Centro semántico de la sala en celdas. Promedia los
        /// <see cref="TileMarker"/> con <see cref="TileType.Floor"/>; si no hay
        /// floors, cae a todos los markers; si tampoco hay, devuelve (0,0,0).
        /// </summary>
        public static Vector3 ComputeRoomCenterCell(RoomLayout room)
        {
            if (room == null) return Vector3.zero;
            var markers = room.GetComponentsInChildren<TileMarker>(includeInactive: true);
            if (markers.Length == 0) return Vector3.zero;

            float fx = 0f, fz = 0f;
            int floorCount = 0;
            float ax = 0f, az = 0f;
            int anyCount = 0;

            foreach (var m in markers)
            {
                if (m == null) continue;
                anyCount++;
                ax += m.Coord.X;
                az += m.Coord.Y;
                if (m.Type == TileType.Floor)
                {
                    floorCount++;
                    fx += m.Coord.X;
                    fz += m.Coord.Y;
                }
            }

            if (floorCount > 0)
                return new Vector3(fx / floorCount, 0f, fz / floorCount);
            if (anyCount > 0)
                return new Vector3(ax / anyCount, 0f, az / anyCount);
            return Vector3.zero;
        }
    }
}
