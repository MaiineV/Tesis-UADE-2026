using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Grid
{
    [CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Renderer Registrar Bootstrap",
        fileName = "TileRendererRegistrarBootstrap")]
    public sealed class TileRendererRegistrarBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 81;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            // Registrar en ServiceLocator con scope Run para que ClearScope(Run) en
            // EndRun lo disponga (TileRendererRegistrar implementa IDisposable y
            // desuscribe del OnRoomEntered ahi). Sin esto, el old registrar sigue
            // suscripto y duplicariamos handlers en cada nueva run.
            var instance = new TileRendererRegistrar();
            ServiceLocator.AddService<TileRendererRegistrar>(instance, ServiceScope.Run);
        }
    }
}
