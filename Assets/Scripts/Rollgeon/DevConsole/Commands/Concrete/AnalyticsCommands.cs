using System.Collections.Generic;
using Rollgeon.Analytics;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Diagnóstico y control de la telemetría UGS (Feature#0029):
    /// <c>analytics status|opt-in|opt-out|reset|delete|test</c>.
    /// Habla solo con los contratos del asm principal — este assembly no
    /// referencia el SDK de UGS.
    /// </summary>
    public sealed class AnalyticsCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("status|opt-in|opt-out|reset|delete|test", ArgKind.String),
        };

        public override string Name => "analytics";
        public override string Description =>
            "Telemetría UGS: analytics status | opt-in | opt-out | reset (re-pregunta) | delete (borrar datos) | test (evento de prueba).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args == null || args.Count == 0)
            {
                return CommandResult.Fail("Usá 'analytics status|opt-in|opt-out|reset|delete|test'.");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status": return Status(ctx);
                case "opt-in": return SetConsent(ctx, granted: true);
                case "opt-out": return SetConsent(ctx, granted: false);
                case "reset": return ResetDecision(ctx);
                case "delete": return Delete(ctx);
                case "test": return SendTest(ctx);
                default: return CommandResult.Fail($"Subcomando desconocido '{args[0]}'. Usá status|opt-in|opt-out|reset|delete|test.");
            }
        }

        private static CommandResult Status(IDevConsoleContext ctx)
        {
            var hasGateway = ctx.TryResolve<IAnalyticsGateway>(out var gateway) && gateway != null;
            var hasSink = ctx.TryResolve<IAnalyticsSink>(out var sink) && sink != null;
            var hasConsent = ctx.TryResolve<IAnalyticsConsentService>(out var consent) && consent != null;

            var initialized = hasGateway && gateway.Initialized;
            var ready = hasSink && sink.Ready;
            var consentLabel = !hasConsent ? "sin servicio"
                : !consent.HasDecision ? "sin decidir"
                : consent.IsGranted ? "granted" : "denied";

            return CommandResult.Ok(
                $"UGS init: {(initialized ? "OK" : "NO (¿proyecto sin linkear a Unity Cloud o sin red?)")} | " +
                $"sink ready: {ready} | consent: {consentLabel} | " +
                $"eventos dropeados: {(hasSink ? sink.DroppedEvents.ToString() : "n/a")}");
        }

        private static CommandResult SetConsent(IDevConsoleContext ctx, bool granted)
        {
            if (!RequireService<IAnalyticsConsentService>(ctx, out var consent, out var error)) return error;
            consent.SetConsent(granted);
            return CommandResult.Ok(granted
                ? "Consentimiento otorgado — la telemetría fluye si UGS inicializó."
                : "Consentimiento revocado — no se envía más telemetría.");
        }

        private static CommandResult ResetDecision(IDevConsoleContext ctx)
        {
            // También revocar a nivel SDK: sin esto el engine seguiría con el
            // consent anterior (persistido por EndUserConsent) hasta la próxima
            // decisión, y el SDK recolectaría sus eventos default sin decisión vigente.
            if (ctx.TryResolve<IAnalyticsGateway>(out var gateway) && gateway != null && gateway.Initialized)
            {
                gateway.ApplyConsent(false);
            }

            AnalyticsPrefs.ClearDecision();
            return CommandResult.Ok("Decisión de consentimiento borrada — el popup re-pregunta al volver al menú.");
        }

        private static CommandResult Delete(IDevConsoleContext ctx)
        {
            if (!RequireService<IAnalyticsConsentService>(ctx, out var consent, out var error)) return error;
            return consent.TryRequestDataDeletion()
                ? CommandResult.Ok("Borrado de datos solicitado al backend de UGS.")
                : CommandResult.Fail("No se pudo solicitar el borrado — ¿UGS sin inicializar?");
        }

        private static CommandResult SendTest(IDevConsoleContext ctx)
        {
            if (!RequireService<IAnalyticsSink>(ctx, out var sink, out var error)) return error;
            if (!sink.Ready)
            {
                return CommandResult.Fail("Sink no listo (init pendiente/fallido o sin consentimiento) — el evento se dropearía.");
            }

            // 'debug_ping' no está declarado en el Event Manager a propósito:
            // llega al Event Browser marcado inválido, suficiente como prueba de
            // conectividad sin ensuciar los datos de balance.
            sink.Send("debug_ping", new Dictionary<string, object> { ["source"] = "devconsole" });
            sink.Flush();
            return CommandResult.Ok("Evento 'debug_ping' enviado + flush. Miralo en el Event Browser (~minutos, aparece como inválido).");
        }
    }
}
