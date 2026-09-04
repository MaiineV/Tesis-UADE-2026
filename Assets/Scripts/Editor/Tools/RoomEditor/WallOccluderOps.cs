using System.Collections.Generic;
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

        /// <summary>Conteos de un <see cref="BakeRoom"/>, separados por origen.</summary>
        public struct BakeSummary
        {
            public int WallsAdded, WallsUpdated, WallsSkipped;
            public int PropsUpdated, PropsSkipped;

            public override string ToString() =>
                $"walls: {WallsAdded} added, {WallsUpdated} updated, {WallsSkipped} ok — " +
                $"props: {PropsUpdated} updated, {PropsSkipped} ok";
        }

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

            return ApplyDirection(occluder, direction);
        }

        /// <summary>Wall tile ya bakeado, en espacio de celda, para que los props copien su dirección.</summary>
        public readonly struct WallRef
        {
            public readonly Vector3 CellPos;
            public readonly WallDirection Direction;

            public WallRef(Vector3 cellPos, WallDirection direction)
            {
                CellPos = cellPos;
                Direction = direction;
            }
        }

        /// <summary>
        /// Fija la <see cref="WallOccluder.Direction"/> de un occluder que vive en
        /// un prop (antorcha, cartel) y no en un tile: los props traen el occluder
        /// desde su propio prefab con una dirección fija, así que sin este paso
        /// todas las instancias de una sala se ocultan como si estuvieran en la
        /// misma pared. El prop copia la dirección del wall tile más cercano —
        /// inferir su propio octante no sirve, porque un prop a mitad de la pared
        /// W puede caer en SW mientras el tile que lo sostiene quedó en W (o al
        /// revés), y entonces se oculta en facings distintos que su pared. Si la
        /// sala no tiene walls bakeados, cae al octante por posición.
        /// Nunca agrega componentes — eso es decisión del prefab.
        /// </summary>
        public static BakeResult EnsureProp(WallOccluder occluder, RoomLayout room,
            IReadOnlyList<WallRef> walls, Vector3 centerCell)
        {
            if (occluder == null || room == null) return BakeResult.Skipped;

            var cellPos = WorldToCellPos(occluder.transform.position, room);
            return ApplyDirection(occluder, ResolvePropDirection(cellPos, walls, centerCell));
        }

        /// <summary>Dirección del wall más cercano en XZ; octante por posición si no hay walls.</summary>
        public static WallDirection ResolvePropDirection(Vector3 cellPos, IReadOnlyList<WallRef> walls, Vector3 centerCell)
        {
            if (walls == null || walls.Count == 0) return InferDirection(cellPos, centerCell);

            float bestSqr = float.MaxValue;
            WallDirection best = WallDirection.N;
            foreach (var w in walls)
            {
                float dx = w.CellPos.x - cellPos.x;
                float dz = w.CellPos.z - cellPos.z;
                float sqr = dx * dx + dz * dz;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = w.Direction;
            }
            return best;
        }

        /// <summary>Wall tiles con occluder de la sala, en espacio de celda (ignora Layer).</summary>
        public static List<WallRef> CollectWallRefs(RoomLayout room)
        {
            var refs = new List<WallRef>();
            if (room == null) return refs;

            foreach (var m in room.GetComponentsInChildren<TileMarker>(includeInactive: true))
            {
                if (m == null || m.Type != TileType.Wall) continue;
                var occ = m.GetComponent<WallOccluder>();
                if (occ == null) continue;
                refs.Add(new WallRef(new Vector3(m.Coord.X, 0f, m.Coord.Y), occ.Direction));
            }
            return refs;
        }

        /// <summary>
        /// Bakea todos los occluders de la sala: los wall tiles (vía
        /// <see cref="EnsureOccluder"/>) y los props con occluder propio (vía
        /// <see cref="EnsureProp"/>). Los tiles no-wall que traen occluder
        /// (puertas) no se tocan. El caller arma el grupo de Undo.
        /// </summary>
        public static BakeSummary BakeRoom(RoomLayout room)
        {
            var summary = new BakeSummary();
            if (room == null) return summary;

            var markers = room.GetComponentsInChildren<TileMarker>(includeInactive: true);
            foreach (var m in markers)
            {
                if (m == null || m.Type != TileType.Wall) continue;
                var cell = new Vector3Int(m.Coord.X, m.Layer, m.Coord.Y);
                switch (EnsureOccluder(m.gameObject, room, cell))
                {
                    case BakeResult.Added: summary.WallsAdded++; break;
                    case BakeResult.Updated: summary.WallsUpdated++; break;
                    case BakeResult.Skipped: summary.WallsSkipped++; break;
                }
            }

            // Después del loop de walls: los props copian direcciones ya bakeadas.
            BakeProps(room, ref summary);
            return summary;
        }

        /// <summary>
        /// Solo el pase de props: los walls se leen tal cual están. Útil para
        /// arreglar antorchas/carteles de salas ya autoradas sin re-inferir
        /// paredes, que en celdas borderline del compás pueden flipear si el
        /// centro de la sala se movió desde el último bake.
        /// </summary>
        public static void BakeProps(RoomLayout room, ref BakeSummary summary)
        {
            if (room == null) return;

            var centerCell = ComputeRoomCenterCell(room);
            var walls = CollectWallRefs(room);
            var occluders = room.GetComponentsInChildren<WallOccluder>(includeInactive: true);
            foreach (var occ in occluders)
            {
                if (occ == null || !IsPropOccluder(occ)) continue;
                switch (EnsureProp(occ, room, walls, centerCell))
                {
                    case BakeResult.Updated: summary.PropsUpdated++; break;
                    default: summary.PropsSkipped++; break;
                }
            }
        }

        // Walls ya se bakearon por celda; las puertas traen su occluder con
        // ManualOverride desde Door.prefab y las dirige el DoorController. Todo
        // lo demás (sin marker, o pintado como Decoration/Interactable) es prop.
        private static bool IsPropOccluder(WallOccluder occluder)
        {
            var marker = occluder.GetComponent<TileMarker>();
            if (marker == null) return true;
            return marker.Type != TileType.Wall && marker.Type != TileType.Door;
        }

        private static BakeResult ApplyDirection(WallOccluder occluder, WallDirection direction)
        {
            if (occluder.ManualOverride) return BakeResult.Skipped;
            if (occluder.Direction == direction) return BakeResult.Skipped;

            Undo.RecordObject(occluder, UndoLabel);
            occluder.Direction = direction;
            EditorUtility.SetDirty(occluder);
            return BakeResult.Updated;
        }

        /// <summary>
        /// Posición world → coordenada de celda continua, con el mismo convenio
        /// que <c>RoomEditorWindow.CellCenter</c>: el centro de la celda
        /// <c>(x, z)</c> está en <c>origin + (x + 0.5, z + 0.5) * TileSize</c>.
        /// Así un prop centrado en un tile cae exactamente en la celda del tile.
        /// </summary>
        public static Vector3 WorldToCellPos(Vector3 world, RoomLayout room)
        {
            var origin = room.GetOrigin();
            float size = Mathf.Max(room.TileSize, 0.01f);
            return new Vector3(
                (world.x - origin.x) / size - 0.5f,
                0f,
                (world.z - origin.z) / size - 0.5f);
        }

        /// <summary>
        /// Quantizes the vector from <paramref name="centerCell"/> to
        /// <paramref name="cell"/> into one of 8 compass octants. Operates in
        /// cell space — invariant under <see cref="RoomLayout.GridOrigin"/>.
        /// Unity convention: +Z = N, +X = E.
        /// </summary>
        public static WallDirection InferDirection(Vector3Int cell, Vector3 centerCell) =>
            InferDirection((Vector3)cell, centerCell);

        /// <summary>
        /// Variante continua de <see cref="InferDirection(Vector3Int, Vector3)"/>
        /// para props que no están alineados a la grilla.
        /// </summary>
        public static WallDirection InferDirection(Vector3 cellPos, Vector3 centerCell)
        {
            float dx = cellPos.x - centerCell.x;
            float dz = cellPos.z - centerCell.z;

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
