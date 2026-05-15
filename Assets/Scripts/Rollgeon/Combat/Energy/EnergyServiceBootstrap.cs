using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.EnergyLib
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para arrastrar el <see cref="EnergyService"/> al
    /// inspector de <c>ServiceBootstrapSO.ExtraServices</c>. Thin — su única responsabilidad
    /// es instanciar <see cref="EnergyService"/> y delegar <see cref="IPreloadableService.Register"/>.
    /// </summary>
    /// <remarks>
    /// Patrón idéntico al <c>TurnManagerBootstrap</c>. Priority 50 — corre antes que el
    /// TurnManager (60) que depende de IEnergyService.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Energy Service Bootstrap",
        fileName = "EnergyServiceBootstrap")]
    public sealed class EnergyServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private EnergyService _instance;

        public int Priority => 50;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new EnergyService();
            _instance.Register();
        }
    }
}
