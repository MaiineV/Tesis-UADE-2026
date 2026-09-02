using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combos.Rules
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Priority 55: antes de InventoryService (60), que registra las reglas de los items
    /// al agregarlos.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Combo Rule Service Bootstrap",
        fileName = "ComboRuleServiceBootstrap")]
    public sealed class ComboRuleServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ComboRuleService _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new ComboRuleService();
            _instance.Register();
        }
    }
}
