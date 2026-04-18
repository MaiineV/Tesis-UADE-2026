using System;
using Patterns;

namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// Centraliza los callbacks de eventos de run lifecycle (<c>OnRunStart</c> / <c>OnRunEnd</c>)
    /// que el <see cref="BootstrapRunner"/> mantiene vivos durante toda la sesion para
    /// delegar en <c>RunBootstrapper</c> (stub — implementado en Sprint 04 / T98) y
    /// hacer <see cref="ServiceLocator.ClearScope(ServiceScope)"/> de <see cref="ServiceScope.Run"/>
    /// al terminar. Plan §4.6, TECHNICAL.md §1.1.3.a.
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

            // [STUB] RunBootstrapper.StartRun(runId, rulesetId) — implementado en Sprint 04 / T98.
            // Cuando el RunBootstrapper real exista, agregarlo como IPreloadableService o invocarlo aqui.
        }

        // Schema EventName.OnRunEnd: args = [Guid runId, RunOutcome outcome]
        private static void OnRunEnd(params object[] args)
        {
            Guid runId = Guid.Empty;
            object outcome = null;

            if (args != null)
            {
                if (args.Length > 0 && args[0] is Guid g) runId = g;
                if (args.Length > 1) outcome = args[1];
            }

            BootstrapLog.Info($"OnRunEnd received — runId={runId}, outcome={outcome ?? "<null>"}. Clearing ServiceScope.Run");

            ServiceLocator.ClearScope(ServiceScope.Run);

            // [STUB] RunBootstrapper.EndRun(runId, outcome) + SaveSystem.Flush() — Sprint 04 / T98 / §15.
        }
    }
}
