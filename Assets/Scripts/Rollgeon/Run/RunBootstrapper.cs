using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Run
{
    /// <summary>
    /// Static entry point for run lifecycle (TECHNICAL.md §1.1.3).
    /// <para>
    /// <see cref="StartRun"/> creates and registers the <see cref="RunContext"/> in
    /// <see cref="ServiceScope.Run"/>, wires the player service, and fires
    /// <see cref="EventName.OnRunStart"/>.
    /// </para>
    /// <para>
    /// <see cref="EndRun"/> tears everything down, fires <see cref="EventName.OnRunEnd"/>,
    /// and calls <see cref="ServiceLocator.ClearScope"/> to dispose all run-scoped services.
    /// </para>
    /// </summary>
    public static class RunBootstrapper
    {
        /// <summary>
        /// Starts a new run: creates <see cref="RunContext"/>, registers it, sets up the
        /// player, and fires <see cref="EventName.OnRunStart"/>.
        /// </summary>
        /// <param name="selected">Hero class chosen by the player.</param>
        /// <param name="ruleset">Active ruleset (passed for downstream listeners).</param>
        /// <param name="runId">Unique run identifier.</param>
        /// <param name="builtDiceBag">
        /// Bolsa construida en el BuildSelectionScreen (Fase 2). Debe aplicarse ANTES de
        /// disparar <see cref="EventName.OnRunStart"/>: los listeners run-start (p.ej. el
        /// DiceEnchantmentService que cachea el RuntimeDiceBag) leen
        /// <see cref="IPlayerService.DiceBag"/> y quedarían lockeados al fallback del
        /// hero si la build llegara tarde (BUG-012). <c>null</c> mantiene el fallback de
        /// Fase 1 (StartingDiceBagRef / Resources).
        /// </param>
        public static void StartRun(ClassHeroSO selected, RulesetSO ruleset, Guid runId,
            DiceBagSO builtDiceBag = null)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));

            var context = new RunContext(runId, selected);
            ServiceLocator.AddService<IRunContextService>(context, ServiceScope.Run);

            var playerService = ServiceLocator.GetService<IPlayerService>();
            playerService.SetPlayer(selected, runId);

            // La build de Fase 2 pisa el StartingDiceBagRef inferido por SetPlayer. Va acá
            // (antes del Trigger de OnRunStart) para que el RuntimeDiceBag del enchantment
            // service se inicialice desde la build real y no desde el fallback (BUG-012).
            if (builtDiceBag != null) playerService.SetDiceBag(builtDiceBag);

            // EndRun de la run previa hizo ClearScope(Run) y borro los IPreloadableService
            // run-scoped (Inventory, Phase, Grid, EnemyAIRegistry, ...). Recrearlos aca
            // antes del Trigger asegura que los handlers de OnRunStart puedan resolverlos.
            // Null en tests que llaman StartRun sin pasar por BootstrapRunner.
            ServiceBootstrapSO.Active?.RegisterRunScoped();

            EventManager.Trigger(EventName.OnRunStart, runId, ruleset != null ? ruleset.RulesetId : null);
        }

        /// <summary>
        /// Ends the current run: marks context inactive, clears the player, fires
        /// <see cref="EventName.OnRunEnd"/>, and disposes all <see cref="ServiceScope.Run"/>
        /// services.
        /// </summary>
        /// <param name="runId">Run identifier (passed to event listeners).</param>
        public static void EndRun(Guid runId)
        {
            if (ServiceLocator.TryGetService<IRunContextService>(out var ctx))
            {
                ((RunContext)ctx).EndRun();
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                playerService.ClearPlayer();
            }

            EventManager.Trigger(EventName.OnRunEnd, runId, (object)null);

            ServiceLocator.ClearScope(ServiceScope.Run);
        }
    }
}
