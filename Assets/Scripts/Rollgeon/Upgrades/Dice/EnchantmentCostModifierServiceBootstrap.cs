using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Priority 55: antes de InventoryService (60).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Upgrades/Dice/Enchantment Cost Modifier Service Bootstrap",
        fileName = "EnchantmentCostModifierServiceBootstrap")]
    public sealed class EnchantmentCostModifierServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private EnchantmentCostModifierService _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new EnchantmentCostModifierService();
            _instance.Register();
        }
    }
}
