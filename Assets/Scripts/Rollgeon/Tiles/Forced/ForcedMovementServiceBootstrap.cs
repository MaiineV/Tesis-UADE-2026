using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="ForcedMovementService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tiles/Forced Movement Service Bootstrap",
        fileName = "ForcedMovementServiceBootstrap")]
    public sealed class ForcedMovementServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ForcedMovementService _instance;

        public int Priority => 81;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IForcedMovementService>(out var existing) && existing != null) return;

            _instance = new ForcedMovementService();
            _instance.Register();
        }
    }
}
