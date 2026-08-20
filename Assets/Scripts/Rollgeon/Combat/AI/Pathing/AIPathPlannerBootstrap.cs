using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.AI.Pathing
{
    /// <summary>
    /// Wrapper thin que registra el <see cref="AIPathPlanner"/> (y su tabla de tuning) en
    /// el <c>ServiceBootstrapSO.ExtraServices</c>. El planner resuelve grid y casillas
    /// frescos del ServiceLocator en cada plan — el grid es run-scoped y él es global.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/AI Path Planner Bootstrap",
        fileName = "AIPathPlannerBootstrap")]
    public sealed class AIPathPlannerBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField]
        [Tooltip("Tabla de tuning. Null = defaults del GDD hardcodeados.")]
        private AIPathTuningSO _tuning;

        private AIPathPlanner _instance;

        /// <summary>Después de Grid/Movement/Tiles — lo consumen los nodos de IA (80+).</summary>
        public int Priority => 82;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IAIPathPlanner>(out var existing) && existing != null) return;

            _instance = new AIPathPlanner(tuning: _tuning);
            ServiceLocator.AddService<IAIPathPlanner>(_instance, ServiceScope.Global);
            if (_tuning != null)
                ServiceLocator.AddService<AIPathTuningSO>(_tuning, ServiceScope.Global);
        }
    }
}
