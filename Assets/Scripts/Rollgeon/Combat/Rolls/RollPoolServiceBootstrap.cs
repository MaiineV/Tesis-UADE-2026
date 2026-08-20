using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Rolls
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para arrastrar el <see cref="RollPoolService"/> al
    /// inspector de <c>ServiceBootstrapSO.ExtraServices</c>. Thin — su única responsabilidad
    /// es instanciar <see cref="RollPoolService"/> y delegar <see cref="IPreloadableService.Register"/>.
    /// </summary>
    /// <remarks>
    /// Mismo patrón (y mismo slot de Priority 50) que tenía <c>EnergyServiceBootstrap</c>:
    /// corre antes que el TurnManager (60) que depende de IRollPoolService.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Roll Pool Service Bootstrap",
        fileName = "RollPoolServiceBootstrap")]
    public sealed class RollPoolServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private RollPoolService _instance;

        public int Priority => 50;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new RollPoolService();
            _instance.Register();
        }
    }
}
