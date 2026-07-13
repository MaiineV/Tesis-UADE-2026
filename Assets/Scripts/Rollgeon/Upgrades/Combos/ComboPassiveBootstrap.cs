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
        [Tooltip("Pool de pasivas — resolver UpgradeId → SO al restaurar un save. " +
                 "Debe ser el mismo pool que rolea la tienda.")]
        [SerializeField] private ComboPassivePoolSO _passivePool;

        public int Priority => 86;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new ComboPassiveService(_passivePool);
            service.Register();
        }
    }
}
