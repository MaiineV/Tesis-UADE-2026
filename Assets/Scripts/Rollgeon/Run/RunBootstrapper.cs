using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Heroes;
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
        public static void StartRun(ClassHeroSO selected, RulesetSO ruleset, Guid runId)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));

            var context = new RunContext(runId, selected);
            ServiceLocator.AddService<IRunContextService>(context, ServiceScope.Run);

            var playerService = ServiceLocator.GetService<IPlayerService>();
            playerService.SetPlayer(selected, runId);

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
