using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Priority 55: antes de InventoryService (60).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Upgrades/Dice/Enchantment Weight Modifier Service Bootstrap",
        fileName = "EnchantmentWeightModifierServiceBootstrap")]
    public sealed class EnchantmentWeightModifierServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private EnchantmentWeightModifierService _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new EnchantmentWeightModifierService();
            _instance.Register();
        }
    }
}
