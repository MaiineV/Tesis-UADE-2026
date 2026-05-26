using System;

namespace Rollgeon.Grid
{
    [Serializable]
    public struct NavNode : IEquatable<NavNode>
    {
        public GridCoord Coord;
        public float Height;

        public NavNode(GridCoord coord, float height = 0f)
        {
            Coord = coord;
            Height = height;
        }

        public bool Equals(NavNode other) => Coord.Equals(other.Coord);
        public override bool Equals(object obj) => obj is NavNode n && Equals(n);
        public override int GetHashCode() => Coord.GetHashCode();
        public override string ToString() => $"NavNode({Coord}, h={Height:F2})";
    }
}
