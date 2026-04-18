using System;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — reemplazado por Rollgeon.Movement (foundation de grid / movimiento).
    /// Shape mínimo que <see cref="Selection.TargetQueryContext"/> consume para expresar
    /// la posición del owner sin acoplar esta foundation al GridManager real.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Y;

        public GridCoord(int x, int y) { X = x; Y = y; }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord gc && Equals(gc);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }
}
