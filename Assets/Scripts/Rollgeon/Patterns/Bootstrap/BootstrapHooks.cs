using System;
using Patterns;

namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// Centraliza los callbacks de eventos de run lifecycle (<c>OnRunStart</c> / <c>OnRunEnd</c>)
    /// que el <see cref="BootstrapRunner"/> mantiene vivos durante toda la sesion.
    /// <see cref="RunBootstrapper"/> owns the actual lifecycle — these handlers only log.
    /// Plan §4.6, TECHNICAL.md §1.1.3.a.
    /// <para>
    /// <b>Idempotente.</b> <see cref="Install"/> y <see cref="Uninstall"/> se pueden invocar
    /// multiples veces sin duplicar suscripciones — protegidos por la flag <see cref="_installed"/>.
    /// </para>
    /// </summary>
    public static class BootstrapHooks
    {
        private static bool _installed;

        /// <summary>
        /// Suscribe los handlers al <see cref="EventManager"/>. Idempotente.
        /// </summary>
        public static void Install()
        {
            if (_installed) return;
            EventManager.Subscribe(EventName.OnRunStart, OnRunStart);
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEnd);
            _installed = true;
            BootstrapLog.Info("Hooks installed (OnRunStart, OnRunEnd)");
        }

        /// <summary>
        /// Desuscribe los handlers. Idempotente.
        /// </summary>
        public static void Uninstall()
        {
            if (!_installed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStart);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEnd);
            _installed = false;
            BootstrapLog.Info("Hooks uninstalled");
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        // Schema EventName.OnRunStart: args = [Guid runId, string rulesetId]
        // Delegated to RunBootstrapper — this handler only logs.
        private static void OnRunStart(params object[] args)
        {
            Guid runId = Guid.Empty;
            string rulesetId = null;

            if (args != null)
            {
                if (args.Length > 0 && args[0] is Guid g) runId = g;
                if (args.Length > 1 && args[1] is string s) rulesetId = s;
            }

            BootstrapLog.Info($"OnRunStart received — runId={runId}, rulesetId={rulesetId ?? "<null>"}");

            // RunBootstrapper.StartRun fires this event *after* registering RunContext
            // and setting the player, so downstream listeners can safely resolve
            // IRunContextService / IPlayerService here.
        }

        // Schema EventName.OnRunEnd: args = [Guid runId, RunOutcome outcome]
        // RunBootstrapper.EndRun already calls ClearScope — this handler only logs.
        private static void OnRunEnd(params object[] args)
        {
            Guid runId = Guid.Empty;
            object outcome = null;

            if (args != null)
            {
                if (args.Length > 0 && args[0] is Guid g) runId = g;
                if (args.Length > 1) outcome = args[1];
            }

            BootstrapLog.Info($"OnRunEnd received — runId={runId}, outcome={outcome ?? "<null>"}");

            // ClearScope(Run) is now called by RunBootstrapper.EndRun *after* firing
            // this event — no duplicate clear needed here.
        }
    }
}
