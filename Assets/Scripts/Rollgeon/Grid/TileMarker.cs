using UnityEngine;

namespace Rollgeon.Grid
{
    [AddComponentMenu("Rollgeon/Grid/Tile Marker")]
    public sealed class TileMarker : MonoBehaviour
    {
        [HideInInspector] public GridCoord Coord;
        [HideInInspector] public int Layer;
        [HideInInspector] public Vector3Int Footprint = Vector3Int.one;
        [HideInInspector] public Vector3Int FootprintOffset = Vector3Int.zero;
        public TileType Type = TileType.Floor;

        [Tooltip("Blocker tiles obstruct edges in NavGraph bakes and forbid stacking another blocker on the same cell. Non-blockers stack freely.")]
        public bool IsBlocker;
    }
}
