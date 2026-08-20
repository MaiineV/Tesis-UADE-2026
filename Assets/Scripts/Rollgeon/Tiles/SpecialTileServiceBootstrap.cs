using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="SpecialTileService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Patrón thin de <c>HazardServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tiles/Special Tile Service Bootstrap",
        fileName = "SpecialTileServiceBootstrap")]
    public sealed class SpecialTileServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private SpecialTileService _instance;

        public int Priority => 79;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<ISpecialTileService>(out var existing) && existing != null) return;

            _instance = new SpecialTileService();
            _instance.Register();
        }
    }
}
