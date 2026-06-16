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
