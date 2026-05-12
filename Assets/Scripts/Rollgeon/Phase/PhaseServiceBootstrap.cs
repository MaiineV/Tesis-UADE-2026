using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Phase
{
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Phase Service", fileName = "PhaseServiceBootstrap")]
    public sealed class PhaseServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private PhaseService _instance;

        public int Priority => 10;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            _instance = new PhaseService();
            ServiceLocator.AddService<IPhaseService>(_instance, ServiceScope.Run);
        }
    }
}
