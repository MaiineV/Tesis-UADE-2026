using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Healing
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Priority 55: antes de InventoryService (60), que registra las reglas de los items
    /// (Ayuno) al agregarlos.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Healing Rule Service Bootstrap",
        fileName = "HealingRuleServiceBootstrap")]
    public sealed class HealingRuleServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private HealingRuleService _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new HealingRuleService();
            _instance.Register();
        }
    }
}
