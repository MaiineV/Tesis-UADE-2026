using System;
using UnityEngine;

namespace Rollgeon.Grid
{
    [Serializable]
    public class NavGraphBakeSettings
    {
        [Min(0f)] public float HeightThreshold = 0.5f;
        [Min(0.01f)] public float TileSize = 1f;
    }
}
