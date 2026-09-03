using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Bootstrap SO de <see cref="LeastUsedComboService"/> (Rezagado). Va en la lista de
    /// ExtraServices del <c>ServiceBootstrap</c>, mismo patrón que
    /// <c>DecayingMultiplierServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Least Used Combo (Rezagado)",
        fileName = "LeastUsedComboServiceBootstrap")]
    public sealed class LeastUsedComboServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private LeastUsedComboService _instance;

        public int Priority => LeastUsedComboService.DefaultPriority;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new LeastUsedComboService();
            _instance.Register();
        }
    }
}
