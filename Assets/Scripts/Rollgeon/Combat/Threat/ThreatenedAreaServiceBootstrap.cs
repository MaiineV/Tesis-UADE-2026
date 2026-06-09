using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="ThreatenedAreaService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Mismo patrón thin que
    /// <c>RerollBudgetServiceBootstrap</c>: instancia + delega <see cref="IPreloadableService.Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Threatened Area Service Bootstrap",
        fileName = "ThreatenedAreaServiceBootstrap")]
    public sealed class ThreatenedAreaServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ThreatenedAreaService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new ThreatenedAreaService();
            _instance.Register();
        }
    }
}
