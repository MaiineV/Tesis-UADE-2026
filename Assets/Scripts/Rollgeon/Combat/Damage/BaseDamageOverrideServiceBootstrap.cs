using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Priority 55: antes de InventoryService (60), que registra los overrides de los
    /// items al agregarlos.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Base Damage Override Service Bootstrap",
        fileName = "BaseDamageOverrideServiceBootstrap")]
    public sealed class BaseDamageOverrideServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private BaseDamageOverrideService _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new BaseDamageOverrideService();
            _instance.Register();
        }
    }
}
