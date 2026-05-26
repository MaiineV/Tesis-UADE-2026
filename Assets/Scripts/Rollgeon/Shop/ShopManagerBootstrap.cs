using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Registra <see cref="ShopManagerService"/> como <see cref="IShopManagerService"/>.
    /// TECHNICAL.md §17.F. El service se suscribe a <c>OnRoomEntered</c> en su
    /// constructor — no hace falta llamar nada más desde el bootstrap.
    /// </summary>
    /// <remarks>
    /// Priority <b>60</b>: después de Feedback (55) y Audio (50), antes del
    /// <c>DungeonManager</c> que aterriza en scope Run. De esa forma cuando el
    /// dungeon dispara <c>OnRoomEntered</c> al entrar a la start room, el
    /// service ya está suscripto.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Shop/Shop Manager Bootstrap",
        fileName = "ShopManagerBootstrap")]
    public sealed class ShopManagerBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Shop data")]
        [Required, Tooltip("Config global de pricing / slots / restock.")]
        [SerializeField] private ShopConfigSO _config;

        [Required, Tooltip("Pool de ítems usado para rolear slots. MVP: un solo pool global. " +
                           "Cuando aterrice multi-floor, pasa a resolverse por piso.")]
        [SerializeField] private ShopPoolSO _pool;

        private ShopManagerService _instance;

        public int Priority => 60;

        public void Register()
        {
            if (_instance != null) return;

            if (_config == null || _pool == null)
            {
                Debug.LogError("[ShopManagerBootstrap] ShopConfigSO o ShopPoolSO sin asignar — no se registra IShopManagerService.");
                return;
            }

            _instance = new ShopManagerService(_config, _pool);
            ServiceLocator.AddService<IShopManagerService>(_instance, ServiceScope.Global);
        }
    }
}
