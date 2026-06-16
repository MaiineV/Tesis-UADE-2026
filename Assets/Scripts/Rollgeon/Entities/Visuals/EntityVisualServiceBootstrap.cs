using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// SO wrapper que registra <see cref="EntityVisualService"/> como
    /// <see cref="IEntityVisualService"/> y <see cref="IEntityPositionResolver"/>
    /// en el <see cref="ServiceLocator"/>. Priority 85 — después de
    /// <see cref="GridManagerBootstrap"/> (75) y <see cref="MovementServiceBootstrap"/> (78).
    /// </summary>
    /// <remarks>
    /// El prefab visual de cada entidad vive en su propio SO
    /// (<see cref="Heroes.ClassHeroSO.VisualPrefab"/> /
    /// <see cref="Entities.EnemyDataSO.VisualPrefab"/>), por lo que el bootstrap
    /// no necesita slots centralizados.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Visuals/Entity Visual Service Bootstrap",
        fileName = "EntityVisualServiceBootstrap")]
    public sealed class EntityVisualServiceBootstrap : ScriptableObject, IPreloadableService
    {
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

            _instance = new EntityVisualService(grid, movement);
            ServiceLocator.AddService<IEntityVisualService>(_instance, ServiceScope.Run);
            ServiceLocator.AddService<IEntityPositionResolver>(_instance, ServiceScope.Run);
        }
    }
}
