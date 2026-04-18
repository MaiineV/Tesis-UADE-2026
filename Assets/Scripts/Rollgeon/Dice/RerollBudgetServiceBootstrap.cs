using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el
    /// <see cref="RerollBudgetService"/> al <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Thin — su unica responsabilidad es instanciar + delegar
    /// <see cref="IPreloadableService.Register"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que un wrapper SO.</b> Los <c>IPreloadableService</c> se agregan via
    /// drag-and-drop del <c>.asset</c> al inspector de <c>ServiceBootstrapSO</c> —
    /// mismo patron que <c>TurnManagerBootstrap</c> y <c>TurnOrderServiceBootstrap</c>.
    /// </para>
    /// <para>
    /// <b>Priority.</b> Hereda <see cref="RerollBudgetService.Priority"/> (<c>70</c>)
    /// — despues de <c>EnergyServiceBootstrap</c> (50) y <c>TurnManagerBootstrap</c> (60).
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Reroll Budget Service Bootstrap",
        fileName = "RerollBudgetServiceBootstrap")]
    public sealed class RerollBudgetServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private RerollBudgetService _instance;

        /// <summary>Matchea <see cref="RerollBudgetService.Priority"/>.</summary>
        public int Priority => 70;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new RerollBudgetService();
            _instance.Register();
        }
    }
}
