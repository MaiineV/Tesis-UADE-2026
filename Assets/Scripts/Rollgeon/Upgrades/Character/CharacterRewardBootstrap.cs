using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Bootstrap del Canal Personaje — crea y registra el
    /// <see cref="CharacterRewardService"/> con su pool + pedestal prefab.
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> 87 — después de <c>ComboCountersService</c> (80) y los
    /// otros canales de upgrades. El service no depende directamente de ellos
    /// pero el orden mantiene la familia de servicios agrupada.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Character/Character Reward Bootstrap",
        fileName = "CharacterRewardBootstrap")]
    public sealed class CharacterRewardBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Pool + Prefab")]
        [Required, SerializeField]
        private CharacterRewardPoolSO _pool;

        [Required, SerializeField]
        [Tooltip("Prefab del pedestal. Debe tener CharacterRewardPedestalInteractable + Collider. " +
                 "MVP: cubo + componente.")]
        private GameObject _pedestalPrefab;

        [Title("Configuration")]
        [MinValue(1)]
        [Tooltip("Cantidad de opciones que aparecen en una sala de boss. GDD: 3.")]
        [SerializeField]
        private int _slotsPerBoss = 3;

        [Tooltip("Offset local del CharacterRewardSO.WorldPrefab sobre el pedestal — " +
                 "default Y=1.5 (encima del cubo). Mismo rol que ShopConfigSO.ItemVisualLocalOffset.")]
        [SerializeField]
        private Vector3 _visualOffset = new Vector3(0f, 1.5f, 0f);

        public int Priority => 87;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new CharacterRewardService(_pool, _pedestalPrefab, _slotsPerBoss, _visualOffset);
            ServiceLocator.AddService<ICharacterRewardService>(service, ServiceScope.Global);
        }
    }
}
