using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra al
    /// <see cref="ForcedRerollCapabilityService"/> al <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Mismo patron que <c>FirstRollTrackerBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Forced Reroll Capability",
        fileName = "ForcedRerollCapabilityBootstrap")]
    public sealed class ForcedRerollCapabilityBootstrap : ScriptableObject, IPreloadableService
    {
        private ForcedRerollCapabilityService _instance;

        /// <summary>Matchea <see cref="ForcedRerollCapabilityService.DefaultPriority"/>.</summary>
        public int Priority => ForcedRerollCapabilityService.DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            if (_instance != null) return;
            _instance = new ForcedRerollCapabilityService();
            _instance.Register();
        }
    }
}
