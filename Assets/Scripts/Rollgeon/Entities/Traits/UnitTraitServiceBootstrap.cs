using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="UnitTraitService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Mismo patrón thin que
    /// <c>HazardServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Unit Trait Service Bootstrap",
        fileName = "UnitTraitServiceBootstrap")]
    public sealed class UnitTraitServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private UnitTraitService _instance;

        public int Priority => 74;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IUnitTraitService>(out var existing) && existing != null) return;

            _instance = new UnitTraitService();
            _instance.Register();
        }
    }
}
