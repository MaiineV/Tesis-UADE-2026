using UnityEngine;

namespace Rollgeon.Grid
{
    [AddComponentMenu("Rollgeon/Grid/Tile Marker")]
    public sealed class TileMarker : MonoBehaviour
    {
        [HideInInspector] public GridCoord Coord;
        [HideInInspector] public int Layer;
    }
}
