using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="RoomSpecialTilesLoader"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tiles/Room Special Tiles Loader Bootstrap",
        fileName = "RoomSpecialTilesLoaderBootstrap")]
    public sealed class RoomSpecialTilesLoaderBootstrap : ScriptableObject, IPreloadableService
    {
        private RoomSpecialTilesLoader _instance;

        public int Priority => 83;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<RoomSpecialTilesLoader>(out var existing) && existing != null) return;

            _instance = new RoomSpecialTilesLoader();
            _instance.Register();
        }
    }
}
