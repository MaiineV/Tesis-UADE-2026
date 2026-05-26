using System;
using System.Collections.Generic;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Coordenada entera (x, y) de la grilla de combate. TECHNICAL.md §17.§I.
    /// </summary>
    /// <remarks>
    /// Struct chico + <see cref="IEquatable{T}"/> para performance en lookups
    /// (<c>Dictionary&lt;GridCoord, Guid&gt;</c> es el patrón de ocupancia).
    /// </remarks>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridCoord Zero => new GridCoord(0, 0);

        /// <summary>4-neighborhood (N, S, E, W).</summary>
        public IEnumerable<GridCoord> Neighbors4()
        {
            yield return new GridCoord(X, Y + 1);
            yield return new GridCoord(X + 1, Y);
            yield return new GridCoord(X, Y - 1);
            yield return new GridCoord(X - 1, Y);
        }

        /// <summary>Distancia Manhattan — métrica default para rango de movimiento 4-grid.</summary>
        public int Manhattan(GridCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        /// <summary>Distancia Chebyshev — alternativa para rango octogonal.</summary>
        public int Chebyshev(GridCoord other) =>
            Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public static GridCoord operator +(GridCoord a, GridCoord b) =>
            new GridCoord(a.X + b.X, a.Y + b.Y);

        public static GridCoord operator -(GridCoord a, GridCoord b) =>
            new GridCoord(a.X - b.X, a.Y - b.Y);

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);

        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridCoord gc && Equals(gc);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X},{Y})";
    }
}
