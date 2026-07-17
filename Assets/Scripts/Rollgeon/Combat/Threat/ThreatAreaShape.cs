using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>Forma del área telegráfica. Cada Boss usa una distinta (Sistemas prerequisito Bosses §1).</summary>
    public enum ThreatShape
    {
        /// <summary>Cuadrado (2·radio+1) centrado en el jugador. Boss 1 — cruz/área 3×3 (radio 1).</summary>
        SquareAroundPlayer,

        /// <summary>Franja horizontal: la(s) fila(s) del jugador. Boss 2 — franja.</summary>
        Row,

        /// <summary>Franja vertical: la(s) columna(s) del jugador. Boss 2 — franja.</summary>
        Column,

        /// <summary>Mitad de la sala donde está el jugador. Boss 3 — media sala.</summary>
        HalfRoom,

        /// <summary>
        /// Banda direccional que sale del propio boss hacia el jugador — Boss 1 (slash).
        /// A diferencia de las shapes de arriba, el origen es la coordenada del boss, no la
        /// del jugador. Ver <see cref="ThreatAreaShape.ComputeDirectionalBand"/>.
        /// </summary>
        DirectionalBand,

        /// <summary>
        /// Varios cuadrados independientes en posiciones erráticas de la sala — Boss 1
        /// (lluvia de zonas). Ni el jugador ni el boss son el centro; ver
        /// <see cref="ThreatAreaShape.ComputeScatteredSquares"/>.
        /// </summary>
        ScatteredSquares,
    }

    /// <summary>Eje de corte para <see cref="ThreatShape.HalfRoom"/>.</summary>
    public enum HalfRoomAxis
    {
        /// <summary>Corte vertical → mitades izquierda/derecha (por X).</summary>
        Vertical,

        /// <summary>Corte horizontal → mitades inferior/superior (por Y).</summary>
        Horizontal,
    }

    /// <summary>
    /// Calcula el conjunto de casillas de un área telegráfica a partir de la posición del jugador
    /// y la forma elegida. Solo devuelve casillas que existen en la grilla (<c>InBounds</c> +
    /// <c>IsWalkable</c>). Es código puro — sin estado — para que los nodos de AI y los tests lo reusen.
    /// </summary>
    public static class ThreatAreaShape
    {
        /// <summary>
        /// Devuelve las casillas amenazadas. <paramref name="size"/> es el radio para
        /// <see cref="ThreatShape.SquareAroundPlayer"/> (1 ⇒ 3×3) y el ancho (en casillas) de la
        /// franja para <see cref="ThreatShape.Row"/> / <see cref="ThreatShape.Column"/>
        /// (1 ⇒ la línea del jugador; 3 ⇒ ±1). Ignorado para <see cref="ThreatShape.HalfRoom"/>.
        /// </summary>
        public static HashSet<GridCoord> Compute(
            IGridManager grid, GridCoord center, ThreatShape shape, int size, HalfRoomAxis axis)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            switch (shape)
            {
                case ThreatShape.SquareAroundPlayer:
                {
                    int r = size < 0 ? 0 : size;
                    for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = new GridCoord(center.X + dx, center.Y + dy);
                        if (IsValidTile(grid, c)) result.Add(c);
                    }
                    break;
                }

                case ThreatShape.Row:
                {
                    int half = HalfBand(size);
                    foreach (var c in RoomTiles(grid))
                        if (System.Math.Abs(c.Y - center.Y) <= half) result.Add(c);
                    break;
                }

                case ThreatShape.Column:
                {
                    int half = HalfBand(size);
                    foreach (var c in RoomTiles(grid))
                        if (System.Math.Abs(c.X - center.X) <= half) result.Add(c);
                    break;
                }

                case ThreatShape.HalfRoom:
                {
                    AddHalfRoom(grid, center, axis, result);
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Banda direccional que sale de <paramref name="self"/> (el boss) hacia
        /// <paramref name="player"/>: <paramref name="depth"/> pasos de profundidad en la
        /// dirección cardinal dominante (<see cref="Cardinal.FromDelta"/>), cada uno una
        /// banda perpendicular de <c>2·halfWidth+1</c> casillas centrada en el eje
        /// perpendicular del boss. Arranca pegada al boss (paso 1), nunca incluye su propia
        /// fila/columna en el eje de avance.
        /// </summary>
        public static HashSet<GridCoord> ComputeDirectionalBand(
            IGridManager grid, GridCoord self, GridCoord player, int halfWidth, int depth)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            int hw = halfWidth < 0 ? 0 : halfWidth;
            int d = depth < 1 ? 1 : depth;

            var dir = CardinalExtensions.FromDelta(self, player);
            var (stepX, stepY) = DirectionStep(dir);
            bool advancesOnX = stepX != 0;

            for (int step = 1; step <= d; step++)
            {
                int originX = self.X + stepX * step;
                int originY = self.Y + stepY * step;

                for (int off = -hw; off <= hw; off++)
                {
                    var c = advancesOnX
                        ? new GridCoord(originX, originY + off)
                        : new GridCoord(originX + off, originY);
                    if (IsValidTile(grid, c)) result.Add(c);
                }
            }

            return result;
        }

        /// <summary>
        /// <paramref name="count"/> cuadrados de <paramref name="squareWidth"/>·<paramref name="squareWidth"/>
        /// casillas, anclados al azar (vía <paramref name="rng"/>) en el 50% central de la sala
        /// (25% de margen recortado en cada borde) — ni el jugador ni el boss son el centro, y
        /// las zonas no aparecen pegadas a las paredes. Requiere una sala con bounds reales (como
        /// <see cref="ThreatShape.Row"/>/<see cref="ThreatShape.Column"/>/<see cref="ThreatShape.HalfRoom"/>);
        /// grafo vacío ⇒ vacío. Los cuadrados pueden solaparse entre sí, se fusionan en el
        /// mismo <c>HashSet</c> sin duplicar.
        /// </summary>
        public static HashSet<GridCoord> ComputeScatteredSquares(
            IGridManager grid, System.Random rng, int count, int squareWidth)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null || rng == null || count <= 0) return result;

            int w = squareWidth < 1 ? 1 : squareWidth;
            var room = new List<GridCoord>(RoomTiles(grid));
            if (room.Count == 0) return result;

            var anchorPool = CenterAnchorPool(room);

            for (int i = 0; i < count; i++)
            {
                var anchor = anchorPool[rng.Next(anchorPool.Count)];
                for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < w; dy++)
                {
                    var c = new GridCoord(anchor.X + dx, anchor.Y + dy);
                    if (IsValidTile(grid, c)) result.Add(c);
                }
            }

            return result;
        }

        // Recorta un margen del 25% por lado, dejando el 50% central de la sala como pool
        // de anclaje — así las zonas erráticas caen "en el medio del mapa", nunca pegadas
        // al borde. Sala minúscula donde el margen vacía el pool ⇒ fallback a la sala entera.
        private static List<GridCoord> CenterAnchorPool(List<GridCoord> room)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in room)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            int marginX = (maxX - minX + 1) / 4;
            int marginY = (maxY - minY + 1) / 4;
            int loX = minX + marginX, hiX = maxX - marginX;
            int loY = minY + marginY, hiY = maxY - marginY;

            var pool = new List<GridCoord>();
            foreach (var c in room)
                if (c.X >= loX && c.X <= hiX && c.Y >= loY && c.Y <= hiY) pool.Add(c);

            return pool.Count > 0 ? pool : room;
        }

        private static (int dx, int dy) DirectionStep(Cardinal dir) => dir switch
        {
            Cardinal.North => (0, 1),
            Cardinal.South => (0, -1),
            Cardinal.East => (1, 0),
            Cardinal.West => (-1, 0),
            _ => (0, 1),
        };

        // El "ancho" de la franja es impar-céntrico: width 1 ⇒ banda 0 (solo la línea),
        // width 2/3 ⇒ banda 1 (±1), etc. half = (width-1)/2.
        private static int HalfBand(int width)
        {
            if (width <= 1) return 0;
            return (width - 1) / 2;
        }

        private static void AddHalfRoom(IGridManager grid, GridCoord center, HalfRoomAxis axis, HashSet<GridCoord> result)
        {
            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return;

            int min = int.MaxValue, max = int.MinValue;
            foreach (var c in tiles)
            {
                int v = axis == HalfRoomAxis.Vertical ? c.X : c.Y;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            // Punto medio entero. El jugador cae en la mitad baja (<= mid) o alta (> mid).
            int mid = (min + max) / 2;
            int playerV = axis == HalfRoomAxis.Vertical ? center.X : center.Y;
            bool playerInLowHalf = playerV <= mid;

            foreach (var c in tiles)
            {
                int v = axis == HalfRoomAxis.Vertical ? c.X : c.Y;
                bool inLow = v <= mid;
                if (inLow == playerInLowHalf) result.Add(c);
            }
        }

        // Casillas reales de la sala. Si el grafo está poblado, usamos sus nodos (maneja
        // salas no rectangulares y orígenes arbitrarios). Si está vacío (stub "infinito"),
        // no hay extensión que enumerar → vacío; las formas Row/Column/HalfRoom requieren
        // una sala con bounds reales (siempre el caso en combate).
        private static IEnumerable<GridCoord> RoomTiles(IGridManager grid)
        {
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) yield break;
            foreach (var c in graph.AllCoords())
                if (grid.IsWalkable(c)) yield return c;
        }

        private static bool IsValidTile(IGridManager grid, GridCoord c)
            => grid.InBounds(c) && grid.IsWalkable(c);
    }
}
