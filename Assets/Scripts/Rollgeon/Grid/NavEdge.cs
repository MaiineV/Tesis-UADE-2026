using System;

namespace Rollgeon.Grid
{
    [Serializable]
    public struct NavEdge : IEquatable<NavEdge>
    {
        public GridCoord From;
        public GridCoord To;
        public float Cost;

        public NavEdge(GridCoord from, GridCoord to, float cost = 1f)
        {
            From = from;
            To = to;
            Cost = cost;
        }

        public bool Equals(NavEdge other) => From.Equals(other.From) && To.Equals(other.To);
        public override bool Equals(object obj) => obj is NavEdge e && Equals(e);
        public override int GetHashCode() => HashCode.Combine(From, To);
        public override string ToString() => $"NavEdge({From} -> {To}, cost={Cost:F1})";
    }
}
