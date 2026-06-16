using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Random;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat
{
    /// <summary>
    /// <see cref="IPreloadableService"/> que registra el
    /// <see cref="DefaultInitiativeProvider"/> y el <see cref="TurnOrderService"/>
    /// en el <see cref="ServiceLocator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este bootstrap es thin — su única responsabilidad es wiring. Se agrega
    /// al <c>ServiceBootstrapSO.ExtraServices</c> (Foundation#0005). Vive como
    /// <see cref="ScriptableObject"/> para poder ser referenciado desde el SO
    /// del bootstrap.
    /// </para>
    /// <para>
    /// <b>Pre-requisitos.</b>
    /// <list type="bullet">
    ///   <item><c>RulesetSO</c> debe estar registrado en el <see cref="ServiceLocator"/>
    ///     antes que este preloadable corra — prioridad más baja (antes).</item>
    ///   <item>Un <see cref="IEntityRegistry"/> debe estar registrado. Si no
    ///     hay uno real, el bootstrap usa un <see cref="InMemoryEntityRegistry"/>
    ///     vacío como fallback y lo registra él mismo (stub — desaparece cuando
    ///     exista el worktree de Entities).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Turn Order Service", fileName = "TurnOrderServiceBootstrap")]
    public sealed class TurnOrderServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField]
        [Tooltip("Si >0, se usa como seed del DefaultInitiativeRng. Útil para tests " +
                 "y QA reproducible. Default 0 = sin seed (Random real).")]
        private int _rngSeed = 0;

        public int Priority => 100; // después del RulesetSO (Foundation#0005).

        public void Register()
        {
            if (!ServiceLocator.TryGetService<RulesetSO>(out var ruleset))
            {
                throw new InvalidOperationException(
                    "[TurnOrderServiceBootstrap] RulesetSO must be registered before this preloadable runs. " +
                    "Verificá el orden de Priority en ServiceBootstrapSO.");
            }

            if (!ServiceLocator.TryGetService<IEntityRegistry>(out var registry))
            {
                // [STUB] — hasta que exista el worktree de Entities, registramos
                // un InMemoryEntityRegistry vacío para no crashear. En scenes
                // de prueba, el smoke test lo popula vía su propio script.
                registry = new InMemoryEntityRegistry();
                ServiceLocator.AddService<IEntityRegistry>(registry);
            }

            IInitiativeRng rng = _rngSeed > 0
                ? new DefaultInitiativeRng(_rngSeed)
                : new DefaultInitiativeRng();

            var provider = new DefaultInitiativeProvider(registry, rng, ruleset);
            ServiceLocator.AddService<IInitiativeProvider>(provider);

            var service = new TurnOrderService();
            ServiceLocator.AddService<TurnOrderService>(service);
        }
    }
}
