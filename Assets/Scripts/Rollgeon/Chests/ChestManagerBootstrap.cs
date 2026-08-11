using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Registra <see cref="ChestService"/> como <see cref="IChestService"/>,
    /// <see cref="IChestRegistry"/> y <see cref="IMinHpClampProvider"/>. El service
    /// se suscribe a sus eventos en el constructor, igual que el shop.
    /// </summary>
    /// <remarks>
    /// Priority <b>60</b> (como <c>ShopManagerBootstrap</c>): Global, suscripto antes
    /// de que el dungeon Run-scope dispare eventos. El spawn real ocurre recién en
    /// <c>OnCombatStart</c>, cuando grilla y combatientes ya están en su lugar.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Chests/Chest Manager Bootstrap",
        fileName = "ChestManagerBootstrap")]
    public sealed class ChestManagerBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Chest data")]
        [Required, Tooltip("Config global de la mecánica de cofre.")]
        [SerializeField] private ChestConfigSO _config;

        [Required, Tooltip("Pool de recompensas por tier.")]
        [SerializeField] private ChestLootPoolSO _lootPool;

        private ChestService _instance;

        public int Priority => 60;

        public void Register()
        {
            if (_instance != null) return;

            if (_config == null || _lootPool == null)
            {
                Debug.LogError("[ChestManagerBootstrap] ChestConfigSO o ChestLootPoolSO sin asignar — no se registra IChestService.");
                return;
            }

            _instance = new ChestService(_config, _lootPool);
            ServiceLocator.AddService<IChestService>(_instance, ServiceScope.Global);
            ServiceLocator.AddService<IChestRegistry>(_instance, ServiceScope.Global);
        }
    }
}
