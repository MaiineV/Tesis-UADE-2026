using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.FirstRoll
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra al
    /// <see cref="FirstRollTrackerService"/> al <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Mismo patron que <c>ComboCountersServiceBootstrap</c>.
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> Hereda <see cref="FirstRollTrackerService.DefaultPriority"/>
    /// (<c>200</c>) — alta para subscribirse al evento <c>OnRollResolved</c> despues
    /// de los consumidores que evaluan <c>PCFirstRollOfCombat</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/First Roll Tracker",
        fileName = "FirstRollTrackerBootstrap")]
    public sealed class FirstRollTrackerBootstrap : ScriptableObject, IPreloadableService
    {
        private FirstRollTrackerService _instance;

        /// <summary>Matchea <see cref="FirstRollTrackerService.DefaultPriority"/>.</summary>
        public int Priority => FirstRollTrackerService.DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            if (_instance != null) return;
            _instance = new FirstRollTrackerService();
            _instance.Register();
        }
    }
}
