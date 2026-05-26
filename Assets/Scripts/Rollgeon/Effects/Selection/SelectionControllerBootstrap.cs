using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Effects.Selection
{
    [CreateAssetMenu(menuName = "Rollgeon/Selection/Selection Controller Bootstrap",
        fileName = "SelectionControllerBootstrap")]
    public sealed class SelectionControllerBootstrap : ScriptableObject, IPreloadableService
    {
        private SelectionController _instance;

        public int Priority => 79;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            _instance = new SelectionController();
            ServiceLocator.AddService<ISelectionController>(_instance, ServiceScope.Run);
        }
    }
}
