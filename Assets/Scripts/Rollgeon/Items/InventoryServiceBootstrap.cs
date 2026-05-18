using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items
{
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Inventory Service",
        fileName = "InventoryServiceBootstrap")]
    public sealed class InventoryServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [Required]
        [SerializeField] private ItemCatalogSO _catalog;

        [MinValue(1)]
        [SerializeField] private int _maxActiveSlots = 4;

        private InventoryService _instance;

        public int Priority => 60;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            if (_catalog == null)
            {
                Debug.LogError("[InventoryServiceBootstrap] ItemCatalogSO not assigned.");
                return;
            }

            _instance = new InventoryService(_catalog, _maxActiveSlots);
            ServiceLocator.AddService<IInventoryService>(_instance, ServiceScope.Run);
        }
    }
}
