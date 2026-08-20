using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="PoisonService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Patrón de <c>HazardServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Poison Service Bootstrap",
        fileName = "PoisonServiceBootstrap")]
    public sealed class PoisonServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private PoisonService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IPoisonService>(out var existing) && existing != null) return;

            _instance = new PoisonService();
            _instance.Register();
        }
    }
}
