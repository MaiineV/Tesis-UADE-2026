using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// SO wrapper que registra <see cref="EntityVisualService"/> como
    /// <see cref="IEntityVisualService"/> y <see cref="IEntityPositionResolver"/>
    /// en el <see cref="ServiceLocator"/>. Priority 85 — después de
    /// <see cref="GridManagerBootstrap"/> (75) y <see cref="MovementServiceBootstrap"/> (78).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Visuals/Entity Visual Service Bootstrap",
        fileName = "EntityVisualServiceBootstrap")]
    public sealed class EntityVisualServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Placeholder prefabs (opcionales)")]
        [Tooltip("Prefab del hero. Null = primitive Capsule cyan generado en runtime.")]
        [SerializeField] private GameObject _heroPrefab;

        [Tooltip("Prefab default de enemigos. Null = primitive Capsule red.")]
        [SerializeField] private GameObject _enemyPrefab;

        [Tooltip("Prefab de bosses (BaseHP >= 80). Null = cae al _enemyPrefab o Cube magenta.")]
        [SerializeField] private GameObject _bossPrefab;

        private EntityVisualService _instance;

        public int Priority => 85;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            ServiceLocator.TryGetService<IGridManager>(out var grid);
            ServiceLocator.TryGetService<IMovementService>(out var movement);

            if (grid == null)
            {
                Debug.LogError("[EntityVisualServiceBootstrap] IGridManager no registrado — " +
                    "EntityVisualService no se crea.");
                return;
            }

            _instance = new EntityVisualService(grid, movement, _heroPrefab, _enemyPrefab, _bossPrefab);
            ServiceLocator.AddService<IEntityVisualService>(_instance, ServiceScope.Run);
            ServiceLocator.AddService<IEntityPositionResolver>(_instance, ServiceScope.Run);
        }
    }
}
