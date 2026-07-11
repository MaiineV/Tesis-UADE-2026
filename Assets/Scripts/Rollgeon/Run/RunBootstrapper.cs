using System;
using Patterns;
using Patterns.Save;
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
        /// <c>true</c> mientras la run activa es un resume de save. Lo consultan los
        /// servicios Global-lifetime que resetean estado en <c>OnRunStart</c> (p.ej.
        /// el oro) para no pisar el valor restaurado desde disco.
        /// </summary>
        public static bool IsResuming { get; private set; }

        /// <summary>
        /// Starts a new run: creates <see cref="RunContext"/>, registers it, sets up the
        /// player, and fires <see cref="EventName.OnRunStart"/>.
        /// </summary>
        /// <param name="selected">Hero class chosen by the player.</param>
        /// <param name="ruleset">Active ruleset (passed for downstream listeners).</param>
        /// <param name="runId">Unique run identifier.</param>
        /// <param name="builtDiceBag">
        /// Bolsa armada en <c>BuildSelectionScreen</c> (Fase 2). Si no es <c>null</c>,
        /// pisa el <c>StartingDiceBagRef</c> que <see cref="IPlayerService.SetPlayer"/>
        /// infirió del hero, <b>antes</b> de disparar <see cref="EventName.OnRunStart"/>.
        /// </param>
        public static void StartRun(ClassHeroSO selected, RulesetSO ruleset, Guid runId,
            DiceBagSO builtDiceBag = null, bool resume = false)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));

            IsResuming = resume;

            // Run nueva = cache de save vacío. Debe correr ANTES de crear RunContext y
            // de RegisterRunScoped(): SaveSystem.Register auto-restaura desde cache, y
            // sin este Clear la run heredaría floor/inventario/counters de la anterior.
            // En resume es al revés: el cache viene de LoadFromDisk y ese mismo
            // auto-restore es el mecanismo que re-hidrata la run.
            if (!resume)
            {
                SaveSystem.Clear();
            }

            var context = new RunContext(runId, selected);
            ServiceLocator.AddService<IRunContextService>(context, ServiceScope.Run);
            SaveSystem.Register(context);

            var playerService = ServiceLocator.GetService<IPlayerService>();
            playerService.SetPlayer(selected, runId);

            // La build elegida debe pisar el StartingDiceBagRef del hero ANTES de
            // disparar OnRunStart: los servicios run-scoped que siembran estado desde
            // IPlayerService.DiceBag en ese evento (DiceEnchantmentService → RuntimeDiceBag,
            // consumido por EnchantedDiceRoller) verían si no los 5×D6 del Guerrero y
            // tirarían todos los dados como D6 ignorando la build elegida (BUG-012).
            if (builtDiceBag != null)
            {
                playerService.SetDiceBag(builtDiceBag);
            }

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
        /// <param name="runCompleted">
        /// <c>true</c> cuando la run terminó de verdad (victoria/derrota) — borra el
        /// save y apaga el Continue del menú. <c>false</c> en quit mid-run desde el
        /// menú de pausa: el flush de <c>OnRunEnd</c> deja el save listo para resumir.
        /// </param>
        public static void EndRun(Guid runId, bool runCompleted)
        {
            if (ServiceLocator.TryGetService<IRunContextService>(out var ctx))
            {
                ((RunContext)ctx).EndRun();
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                playerService.ClearPlayer();
            }

            // El handler de SaveSystemBootstrap captura + flushea acá (RunEnd).
            EventManager.Trigger(EventName.OnRunEnd, runId, (object)null);

            if (runCompleted)
            {
                SaveSystem.DeleteSave();
            }

            ServiceLocator.ClearScope(ServiceScope.Run);
            IsResuming = false;
        }
    }
}
