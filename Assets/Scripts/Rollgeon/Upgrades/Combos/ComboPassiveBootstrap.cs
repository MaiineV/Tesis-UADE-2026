using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Bootstrap del Canal Combos — crea y registra el <see cref="ComboPassiveService"/>.
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> 86 — después de <c>ComboCountersService</c> (80) y
    /// <c>DiceEnchantmentBootstrap</c> (85). El service no depende directamente
    /// de los otros, pero el orden garantiza que los readers que usen estos
    /// services ya estén disponibles si se invocan al Register.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Combos/Combo Passive Bootstrap",
        fileName = "ComboPassiveBootstrap")]
    public sealed class ComboPassiveBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 86;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new ComboPassiveService();
            service.Register();
        }
    }
}
