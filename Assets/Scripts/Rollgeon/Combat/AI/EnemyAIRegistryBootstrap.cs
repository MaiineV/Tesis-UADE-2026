using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// SO wrapper que registra el <see cref="EnemyAIRegistry"/> como
    /// <see cref="IEnemyAIRegistry"/> en el <see cref="ServiceLocator"/>. Run-scope.
    /// Priority 77 — antes que <see cref="Rollgeon.Combat.Handoff.DefaultEnemySpawnResolver"/>
    /// y el handler driveado por árbol.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/AI/Enemy AI Registry Bootstrap",
        fileName = "EnemyAIRegistryBootstrap")]
    public sealed class EnemyAIRegistryBootstrap : ScriptableObject, IPreloadableService
    {
        private EnemyAIRegistry _instance;

        public int Priority => 77;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            _instance = new EnemyAIRegistry();
            ServiceLocator.AddService<IEnemyAIRegistry>(_instance, ServiceScope.Run);
        }
    }
}
