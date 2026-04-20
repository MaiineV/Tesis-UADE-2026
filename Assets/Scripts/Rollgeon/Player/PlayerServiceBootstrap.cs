using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Player
{
    /// <summary>
    /// Bootstrap wrapper that creates and registers <see cref="PlayerService"/>
    /// into <see cref="ServiceLocator"/> during <c>ServiceBootstrapSO.RegisterAll</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Player Service",
        fileName = "PlayerServiceBootstrap")]
    public sealed class PlayerServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private PlayerService _instance;

        public int Priority => 30;

        /// <inheritdoc />
        public void Register()
        {
            if (_instance != null) return;
            _instance = new PlayerService();
            ServiceLocator.AddService<IPlayerService>(_instance, ServiceScope.Global);
        }
    }
}
