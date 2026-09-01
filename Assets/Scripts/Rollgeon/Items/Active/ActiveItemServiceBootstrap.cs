using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Registra el slot unico de item activo y su servicio de activacion.
    /// </summary>
    /// <remarks>
    /// Scope Run, como el inventario: el item equipado es estado de run y muere con ella.
    /// Priority por encima de <c>InventoryServiceBootstrap</c> (60) porque no depende de
    /// el — el item activo no ocupa un slot de inventario.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Active Item Service",
        fileName = "ActiveItemServiceBootstrap")]
    public sealed class ActiveItemServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [Required]
        [SerializeField]
        [Tooltip("Mismo catalogo que usa el inventario. Hace falta para restaurar el " +
                 "item equipado desde el save por su ItemId.")]
        private ItemCatalogSO _catalog;

        [SerializeField]
        [Tooltip("Pool propio de encantamientos del item activo. Separado de la lista de " +
                 "33 de los dados de combate (GDD §23). Hace falta para restaurar el " +
                 "encantamiento equipado desde el save.")]
        private List<ActiveItemEnchantmentSO> _enchantmentPool = new List<ActiveItemEnchantmentSO>();

        /// <summary>Pool completo, para la Sala de Encantamientos y la dev console.</summary>
        public IReadOnlyList<ActiveItemEnchantmentSO> EnchantmentPool => _enchantmentPool;

        private EquippedActiveItemService _equipped;

        public int Priority => 61;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            if (_catalog == null)
            {
                Debug.LogError("[ActiveItemServiceBootstrap] ItemCatalogSO no asignado — " +
                               "el item activo no se va a poder restaurar del save.");
            }

            _equipped = new EquippedActiveItemService(_catalog, _enchantmentPool);
            ServiceLocator.AddService<IEquippedActiveItemService>(_equipped, ServiceScope.Run);
            SaveSystem.Register(_equipped);

            var activation = new ActiveItemActivationService(_equipped, new ActiveItemDieRoller());
            ServiceLocator.AddService<IActiveItemActivationService>(activation, ServiceScope.Run);
        }
    }
}
