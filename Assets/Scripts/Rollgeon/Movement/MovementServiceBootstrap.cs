using Patterns;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Movement
{
    /// <summary>
    /// SO wrapper que registra <see cref="MovementService"/> como <see cref="IMovementService"/>.
    /// TECHNICAL.md §17.§B.
    /// </summary>
    /// <remarks>
    /// <b>Scope.</b> Run. Depende de <see cref="IGridManager"/> registrado por
    /// <see cref="GridManagerBootstrap"/> (priority 75). Priority 78 para quedar después.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Movement/Movement Service Bootstrap",
        fileName = "MovementServiceBootstrap")]
    public sealed class MovementServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private MovementService _instance;

        public int Priority => 78;

        public void Register()
        {
            if (_instance != null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogError("[MovementServiceBootstrap] IGridManager no registrado — " +
                    "asegurate de que GridManagerBootstrap tenga priority < 78.");
                return;
            }
            _instance = new MovementService(grid);
            ServiceLocator.AddService<IMovementService>(_instance, ServiceScope.Run);
        }
    }
}
