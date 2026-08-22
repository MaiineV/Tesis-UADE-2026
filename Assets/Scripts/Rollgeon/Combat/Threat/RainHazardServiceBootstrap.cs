using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="RainHazardService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Rain Hazard Service Bootstrap",
        fileName = "RainHazardServiceBootstrap")]
    public sealed class RainHazardServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private RainHazardService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new RainHazardService();
            _instance.Register();
        }
    }
}
