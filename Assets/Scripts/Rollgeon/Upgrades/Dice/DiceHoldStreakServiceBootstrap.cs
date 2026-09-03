using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Bootstrap SO de <see cref="DiceHoldStreakService"/>. Va en la lista de
    /// ExtraServices del <c>ServiceBootstrap</c> (mismo patrón que
    /// <see cref="ForcedRerollCapabilityBootstrap"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Dice Hold Streak",
        fileName = "DiceHoldStreakServiceBootstrap")]
    public sealed class DiceHoldStreakServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private DiceHoldStreakService _instance;

        public int Priority => DiceHoldStreakService.DefaultPriority;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new DiceHoldStreakService();
            _instance.Register();
        }
    }
}
