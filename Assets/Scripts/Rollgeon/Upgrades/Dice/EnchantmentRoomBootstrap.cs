using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que crea y registra el
    /// <see cref="EnchantmentRoomService"/> — orquesta las Salas de Encantamiento
    /// (lazy-init via <c>OnRoomEntered</c>, spawn del altar, flow de aplicación).
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> 90 — después de <c>DiceEnchantmentBootstrap</c> (85) ya
    /// que el service consulta <c>IDiceEnchantmentService</c> en el flow.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Enchantment Room Bootstrap",
        fileName = "EnchantmentRoomBootstrap")]
    public sealed class EnchantmentRoomBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Config + Pool")]
        [Required]
        [SerializeField]
        private EnchantmentConfigSO _config;

        [Required]
        [SerializeField]
        private EnchantmentPoolSO _pool;

        [Title("Altar Prefab")]
        [Required]
        [Tooltip("Prefab del altar. Debe tener un EnchantmentAltarInteractable + Collider. " +
                 "Para MVP funcional, alcanza con un cubo + el componente — sin arte final.")]
        [SerializeField]
        private GameObject _altarPrefab;

        public int Priority => 90;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new EnchantmentRoomService(_config, _pool, _altarPrefab);
            ServiceLocator.AddService<IEnchantmentRoomService>(service, ServiceScope.Global);
        }
    }
}
