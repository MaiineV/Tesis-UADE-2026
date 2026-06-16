using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// SO wrapper que registra el <see cref="GridManager"/> como <see cref="IGridManager"/>
    /// en <see cref="ServiceLocator"/>. TECHNICAL.md §17.§I.
    /// </summary>
    /// <remarks>
    /// <b>Scope.</b> <see cref="ServiceScope.Run"/> — la grilla pertenece a la run activa;
    /// al terminarla se clearea y se re-registra la próxima.
    /// <b>Priority.</b> 75 — después de los servicios de dice/reroll (70-72) y antes de
    /// movement (78) y AI (80).
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Grid/Grid Manager Bootstrap", fileName = "GridManagerBootstrap")]
    public sealed class GridManagerBootstrap : ScriptableObject, IPreloadableService
    {
        private GridManager _instance;

        public int Priority => 75;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            _instance = new GridManager();
            ServiceLocator.AddService<IGridManager>(_instance, ServiceScope.Run);
        }
    }
}
