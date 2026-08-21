using System.Collections.Generic;
using System.Text;
using Rollgeon.Combat.Threat;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Rooms
{
    /// <summary>
    /// Diagnóstico de sólo lectura: mide la grilla caminable real de las salas de jefe y dibuja
    /// cómo cae encima la partición en 6 sectores de <see cref="ThreatShape.RoomSector"/>.
    /// </summary>
    /// <remarks>
    /// No muta nada — abre el prefab, lee el <c>NavGraph</c> ya horneado en su <see cref="RoomLayout"/>
    /// y loguea. Existe para poder discutir el diseño de los sectores contra el tamaño real de la
    /// sala en vez de contra una estimación.
    /// </remarks>
    public static class BossRoomMeasure
    {
        private static readonly string[] Rooms =
        {
            "Assets/Prefabs/Rooms/FloorOne/Boss_Room_Croupier.prefab",
            "Assets/Prefabs/Rooms/FloorTwo/Boss_Room_Cajero.prefab",
            "Assets/Prefabs/Rooms/FloorThree/Boss_Room_Generala.prefab",
            "Assets/Prefabs/Rooms/FloorOne/Boss_Room01.prefab",
        };

        [MenuItem("Tools/Rollgeon/Debug/Measure Boss Rooms")]
        public static void Measure()
        {
            var sb = new StringBuilder();

            foreach (var path in Rooms)
            {
                sb.AppendLine("==================================================");
                sb.AppendLine(path);
                sb.AppendLine("==================================================");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { sb.AppendLine("  !! no está el prefab"); continue; }

                var layout = prefab.GetComponentInChildren<RoomLayout>(true);
                if (layout == null) { sb.AppendLine("  !! sin RoomLayout"); continue; }

                var graph = layout.NavGraph;
                if (graph == null || graph.IsEmpty)
                {
                    sb.AppendLine("  !! NavGraph vacío (sin hornear)");
                    continue;
                }

                var coords = new List<GridCoord>();
                foreach (var n in graph.Nodes) coords.Add(n.Coord);

                Bounds2D(coords, out int minX, out int maxX, out int minY, out int maxY);
                int w = maxX - minX + 1;
                int h = maxY - minY + 1;

                sb.AppendLine($"  tiles caminables : {coords.Count}");
                sb.AppendLine($"  bounds           : X [{minX}..{maxX}]  Y [{minY}..{maxY}]");
                sb.AppendLine($"  tamaño           : {w} x {h}   (área {w * h}, huecos {w * h - coords.Count})");
                sb.AppendLine($"  TileSize         : {layout.TileSize}");


                // --- inventario de props y blockers reales ---
                sb.AppendLine("  --- TileMarkers con IsBlocker ---");
                int nb = 0;
                foreach (var m in prefab.GetComponentsInChildren<TileMarker>(true))
                {
                    if (!m.IsBlocker) continue;
                    nb++;
                    sb.AppendLine($"    {m.name,-24} coord=({m.Coord.X},{m.Coord.Y}) " +
                                  $"footprint={m.Footprint.x}x{m.Footprint.z} type={m.Type}");
                }
                if (nb == 0) sb.AppendLine("    (ninguno)");

                sb.AppendLine("  --- props presentes (no-tile) ---");
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.name;
                    if (n.StartsWith("Tile") || n.StartsWith("Corner") || n.StartsWith("Pared")
                        || n.StartsWith("Door") || n == "Piso" || n == "Tiles") continue;
                    var mk = t.GetComponent<TileMarker>();
                    if (mk == null) continue;
                    sb.AppendLine($"    {n,-24} coord=({mk.Coord.X},{mk.Coord.Y}) " +
                                  $"blocker={mk.IsBlocker} footprint={mk.Footprint.x}x{mk.Footprint.z}");
                }
                sb.AppendLine();
                // --- partición en 3 columnas x 2 filas (la del RoomSector) ---
                sb.AppendLine("  --- particion RoomSector (3 col x 2 fil) ---");
                for (int c = 0; c < 3; c++)
                {
                    Band(minX, maxX, c, 3, out int loX, out int hiX);
                    sb.AppendLine($"    columna {c}: X [{loX}..{hiX}]  ancho {hiX - loX + 1}");
                }
                for (int r = 0; r < 2; r++)
                {
                    Band(minY, maxY, r, 2, out int loY, out int hiY);
                    sb.AppendLine($"    fila    {r}: Y [{loY}..{hiY}]  alto  {hiY - loY + 1}");
                }

                // --- costura: filas/columnas que caen en dos bandas a la vez ---
                var seamY = new List<int>();
                Band(minY, maxY, 0, 2, out int b0lo, out int b0hi);
                Band(minY, maxY, 1, 2, out int b1lo, out int b1hi);
                for (int y = minY; y <= maxY; y++)
                    if (y >= b0lo && y <= b0hi && y >= b1lo && y <= b1hi) seamY.Add(y);

                var seamX = new List<int>();
                for (int y = 0; y < 0; y++) { }
                for (int c = 0; c < 2; c++)
                {
                    Band(minX, maxX, c, 3, out int alo, out int ahi);
                    Band(minX, maxX, c + 1, 3, out int blo, out int bhi);
                    for (int x = minX; x <= maxX; x++)
                        if (x >= alo && x <= ahi && x >= blo && x <= bhi && !seamX.Contains(x)) seamX.Add(x);
                }

                sb.AppendLine($"    COSTURA filas   : {(seamY.Count == 0 ? "ninguna" : string.Join(",", seamY))}");
                sb.AppendLine($"    COSTURA columnas: {(seamX.Count == 0 ? "ninguna" : string.Join(",", seamX))}");

                // --- mapa ASCII: dígito = índice de sector 1..6, '.' = hueco ---
                sb.AppendLine("  --- mapa (numero = sector, '.' = no caminable) ---");
                var walk = new HashSet<GridCoord>(coords);
                for (int y = maxY; y >= minY; y--)
                {
                    var line = new StringBuilder($"    y{y,3} |");
                    for (int x = minX; x <= maxX; x++)
                    {
                        var c = new GridCoord(x, y);
                        if (!walk.Contains(c)) { line.Append(" ."); continue; }
                        line.Append(' ').Append(SectorOf(x, y, minX, maxX, minY, maxY));
                    }
                    sb.AppendLine(line.ToString());
                }
                var axis = new StringBuilder("          ");
                for (int x = minX; x <= maxX; x++) axis.Append(' ').Append(Mathf.Abs(x) % 10);
                sb.AppendLine(axis.ToString() + "   <- x");
                sb.AppendLine();
            }

            var outPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "boss_room_measure.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[BossRoomMeasure] escrito en {outPath}");
        }

        /// <summary>Índice de sector 1..6 de una celda; '+' si cae en más de uno (costura).</summary>
        private static char SectorOf(int x, int y, int minX, int maxX, int minY, int maxY)
        {
            int hits = 0, last = 0;
            for (int s = 1; s <= 6; s++)
            {
                int column = (s - 1) % 3;
                int row = s <= 3 ? 1 : 0;
                Band(minX, maxX, column, 3, out int loX, out int hiX);
                Band(minY, maxY, row, 2, out int loY, out int hiY);
                if (x < loX || x > hiX || y < loY || y > hiY) continue;
                hits++; last = s;
            }
            if (hits == 0) return '?';
            return hits > 1 ? '+' : (char)('0' + last);
        }

        private static void Band(int min, int max, int index, int count, out int lo, out int hi)
        {
            int extent = max - min + 1;
            int size = (extent + count - 1) / count;
            bool last = index >= count - 1;
            lo = last ? max - size + 1 : min + index * size;
            hi = last ? max : lo + size - 1;
            if (lo < min) lo = min;
            if (lo > max) lo = max;
            if (hi > max) hi = max;
            if (hi < min) hi = min;
        }

        private static void Bounds2D(
            List<GridCoord> tiles, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue; maxX = int.MinValue;
            minY = int.MaxValue; maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }
        }
    }
}
