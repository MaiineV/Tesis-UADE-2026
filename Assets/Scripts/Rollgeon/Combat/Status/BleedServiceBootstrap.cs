using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="BleedService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Patrón de <c>PoisonServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Bleed Service Bootstrap",
        fileName = "BleedServiceBootstrap")]
    public sealed class BleedServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private BleedService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IBleedService>(out var existing) && existing != null) return;

            _instance = new BleedService();
            _instance.Register();
        }
    }
}
