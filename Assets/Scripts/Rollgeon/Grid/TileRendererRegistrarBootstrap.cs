using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Grid
{
    [CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Renderer Registrar Bootstrap",
        fileName = "TileRendererRegistrarBootstrap")]
    public sealed class TileRendererRegistrarBootstrap : ScriptableObject, IPreloadableService
    {
        private TileRendererRegistrar _instance;

        public int Priority => 81;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new TileRendererRegistrar();
        }
    }
}
