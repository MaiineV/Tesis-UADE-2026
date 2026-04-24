using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Grid
{
    [CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Highlight Service Bootstrap",
        fileName = "TileHighlightServiceBootstrap")]
    public sealed class TileHighlightServiceBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 76;

        public void Register()
        {
            var instance = new TileHighlightService();
            ServiceLocator.AddService<ITileHighlightService>(instance, ServiceScope.Run);
        }
    }
}
