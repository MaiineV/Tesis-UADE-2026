using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="HazardService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Hazard Service Bootstrap",
        fileName = "HazardServiceBootstrap")]
    public sealed class HazardServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private HazardService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;

            // Un segundo HazardService encima dejaría dos suscriptores de OnTurnQueueBuilt vivos:
            // el hazard tickearía dos veces por ronda.
            if (ServiceLocator.TryGetService<IHazardService>(out var existing) && existing != null) return;

            _instance = new HazardService();
            _instance.Register();
        }
    }
}
