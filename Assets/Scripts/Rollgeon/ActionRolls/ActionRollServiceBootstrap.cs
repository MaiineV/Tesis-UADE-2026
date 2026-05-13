using Patterns;
using Rollgeon.Combat.Energy;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que registra el
    /// <see cref="ActionRollService"/> en el <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> <see cref="ServiceScope.Run"/> — la instancia vive lo que vive
    /// el run, sin estado persistente entre runs.
    /// </para>
    /// <para>
    /// <b>Priority.</b> <c>74</c> — despues de <c>RerollBudgetService</c> (70),
    /// <c>DiceRollerBootstrap</c> (72) y <c>EnergyService</c> (50). Necesita ambos
    /// como prerequisito.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Action Rolls/Action Roll Service Bootstrap",
        fileName = "ActionRollServiceBootstrap")]
    public sealed class ActionRollServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ActionRollService _instance;

        public int Priority => 74;

        public void Register()
        {
            if (_instance != null) return;

            if (!ServiceLocator.TryGetService<IDiceRoller>(out var roller) || roller == null)
            {
                Debug.LogError("[ActionRollServiceBootstrap] IDiceRoller no registrado — " +
                               "agregar DiceRollerBootstrap con Priority < 74.");
                return;
            }

            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
            {
                Debug.LogError("[ActionRollServiceBootstrap] IEnergyService no registrado — " +
                               "agregar EnergyService con Priority < 74.");
                return;
            }

            // ComboCatalog es opcional — si no esta registrado, las tiradas resuelven
            // por suma cruda (sin combo) en vez de por BaseDamage del combo (formula B).
            ServiceLocator.TryGetService<ComboCatalogSO>(out var comboCatalog);
            if (comboCatalog == null)
            {
                Debug.LogWarning("[ActionRollServiceBootstrap] ComboCatalogSO no registrado — " +
                                 "las tiradas de Force Door / Heal usaran suma cruda en vez de combos.");
            }

            _instance = new ActionRollService(roller, energy, comboCatalog);
            ServiceLocator.AddService<IActionRollService>(_instance, ServiceScope.Run);
        }
    }
}
