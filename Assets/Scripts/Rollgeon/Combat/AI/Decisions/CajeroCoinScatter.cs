using System.Collections.Generic;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Elige las casillas donde caen las monedas del Cajero: repartidas por la sala, libres, y sin
    /// otra moneda encima.
    /// </summary>
    /// <remarks>
    /// Compartido por la lluvia de la sala y por el empujón: las dos fuentes reparten igual, y dos
    /// sorteos distintos derivaban en dos criterios distintos de "libre" y de "repartida".
    /// </remarks>
    public static class CajeroCoinScatter
    {
        /// <summary>
        /// Hasta <paramref name="count"/> casillas, separadas al menos
        /// <paramref name="minSeparation"/> en Chebyshev. Devuelve menos si la sala no da.
        /// </summary>
        /// <remarks>
        /// El peligro <b>no</b> se filtra: una moneda sobre pinchos es contenido, y levantarla ahí
        /// cuesta lo que cuesta.
        /// </remarks>
        public static List<GridCoord> PickTiles(
            IGridManager grid,
            IHazardService hazards,
            System.Random rng,
            int count,
            int minSeparation)
        {
            var picked = new List<GridCoord>();
            if (grid?.Graph == null || hazards == null || count <= 0) return picked;

            rng ??= new System.Random();

            var pool = new List<GridCoord>();
            foreach (var coord in grid.Graph.AllCoords())
            {
                // IsFree cubre al jugador y al jefe: una moneda debajo de alguien no se puede
                // levantar (la casilla dispara al ENTRAR, y ya está parado ahí).
                if (!grid.IsFree(coord)) continue;

                // Dos monedas apiladas se cobran las dos con un solo paso: los triggers de hazard
                // disparan una vez POR INSTANCIA y nada valida el solape. Se perdería un punto al
                // que ir sin que se note. Los pinchos son casilla especial, no hazard, así que esto
                // no bloquea la moneda sobre pinchos.
                if (hazards.TryGetHazardAt(coord, out _)) continue;

                pool.Add(coord);
            }

            // Orden estable antes de tirar el dado: el grafo no garantiza orden de iteración, y sin
            // esto el mismo seed elegiría casillas distintas entre corridas.
            pool.Sort(CompareCoord);
            Shuffle(pool, rng);

            // Dos pasadas: la primera respeta la separación mínima, la segunda rellena si la sala no
            // tiene lugar para tanta distancia.
            for (int pass = 0; pass < 2 && picked.Count < count; pass++)
            {
                int separation = pass == 0 ? minSeparation : 0;
                foreach (var coord in pool)
                {
                    if (picked.Count >= count) break;
                    if (picked.Contains(coord)) continue;
                    if (!IsFarEnough(coord, picked, separation)) continue;
                    picked.Add(coord);
                }
            }

            return picked;
        }

        private static bool IsFarEnough(GridCoord coord, List<GridCoord> picked, int separation)
        {
            if (separation <= 0) return true;
            foreach (var other in picked)
                if (coord.Chebyshev(other) < separation) return false;
            return true;
        }

        private static void Shuffle(List<GridCoord> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static int CompareCoord(GridCoord a, GridCoord b)
        {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }
    }
}
