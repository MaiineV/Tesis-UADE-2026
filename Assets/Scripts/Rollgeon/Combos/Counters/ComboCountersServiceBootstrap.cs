using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combos.Counters
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="ComboCountersService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Thin — su única responsabilidad es
    /// instanciar el servicio runtime y delegar <see cref="IPreloadableService.Register"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Patrón.</b> Mismo estilo que <c>RerollBudgetServiceBootstrap</c> /
    /// <c>TurnManagerBootstrap</c> — asset drag-and-drop al inspector de
    /// <c>ServiceBootstrapSO</c>.
    /// </para>
    /// <para>
    /// <b>Priority.</b> Hereda <see cref="ComboCountersService.DefaultPriority"/>
    /// (<c>80</c>) — después de Energy (50), TurnManager (60) y RerollBudget (70).
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Combo Counters Service",
        fileName = "ComboCountersServiceBootstrap")]
    public sealed class ComboCountersServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ComboCountersService _instance;

        /// <summary>Matchea <see cref="ComboCountersService.DefaultPriority"/>.</summary>
        public int Priority => ComboCountersService.DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            if (_instance != null) return;
            _instance = new ComboCountersService();
            _instance.Register();
        }
    }
}
