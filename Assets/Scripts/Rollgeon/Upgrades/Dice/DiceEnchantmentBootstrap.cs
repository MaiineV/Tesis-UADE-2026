using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que crea y registra el
    /// <see cref="DiceEnchantmentService"/> en el <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> Global. El service subscribe a <c>OnRunStart</c>/<c>OnRunEnd</c>
    /// para inicializar/liberar el <see cref="RuntimeDiceBag"/> (que sí vive en
    /// scope Run y se libera via <c>ClearScope(Run)</c>).
    /// </para>
    /// <para>
    /// <b>Priority.</b> 85 — después del <c>ComboCountersService</c> (80) por si
    /// algún reader (<c>ReadComboCounter</c>) lo necesita durante la inicialización
    /// (no debería, pero ordena la dependencia).
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Dice Enchantment Bootstrap",
        fileName = "DiceEnchantmentBootstrap")]
    public sealed class DiceEnchantmentBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Config")]
        [Required]
        [SerializeField]
        [Tooltip("Config canónica del altar: costo base, multiplicador de re-encantamiento, " +
                 "umbral de caras mínimas. Sin esto el service no valida bien los applies.")]
        private EnchantmentConfigSO _config;

        public int Priority => 85;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new DiceEnchantmentService(_config);
            service.Register();
        }
    }
}
