using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Weakness
{
    /// <summary>
    /// <see cref="IPreloadableService"/> que registra el <see cref="WeaknessRegistry"/> y el
    /// <see cref="WeaknessChecker"/> en el <see cref="ServiceLocator"/>.
    /// <para>
    /// Patron identico a <c>TurnOrderServiceBootstrap</c>. Se agrega al
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>Pre-requisitos.</b> <see cref="RulesetSO"/> debe estar registrado antes que este
    /// preloadable corra — prioridad mas baja (antes).
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Weakness Service", fileName = "WeaknessServiceBootstrap")]
    public sealed class WeaknessServiceBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 110; // despues del RulesetSO y del TurnOrder (100).

        public void Register()
        {
            if (!ServiceLocator.TryGetService<RulesetSO>(out var ruleset))
            {
                throw new InvalidOperationException(
                    "[WeaknessServiceBootstrap] RulesetSO must be registered before this preloadable runs. " +
                    "Verificá el orden de Priority en ServiceBootstrapSO.");
            }

            IWeaknessRegistry registry;
            if (!ServiceLocator.TryGetService<IWeaknessRegistry>(out registry))
            {
                registry = new WeaknessRegistry();
                ServiceLocator.AddService<IWeaknessRegistry>(registry);
            }

            var checker = new WeaknessChecker(registry, ruleset);
            ServiceLocator.AddService<IWeaknessChecker>(checker);
        }
    }
}
