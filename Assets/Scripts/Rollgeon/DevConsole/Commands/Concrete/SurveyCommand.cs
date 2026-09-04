using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Survey;
using Rollgeon.UI;
using Rollgeon.UI.Screens;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Cuestionario de evento (Feature#0074): estado, abrir a mano, ver/forzar la cola
    /// offline, mandar una respuesta sintética y rearmar el disparo por piso.
    /// </summary>
    public sealed class SurveyCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("status|show|pending|flush|test|reset", ArgKind.String),
        };

        public override string Name => "survey";
        public override string Description => "Encuesta de evento: status | show | pending | flush | test | reset.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args == null || args.Count == 0)
            {
                return CommandResult.Fail("Usá 'survey status|show|pending|flush|test|reset'.");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status": return Status(ctx);
                case "show": return Show(ctx);
                case "pending": return Pending(ctx);
                case "flush": return Flush(ctx);
                case "test": return SendTest(ctx);
                case "reset": return Reset(ctx);
                default: return CommandResult.Fail($"Subcomando desconocido '{args[0]}'. Usá status|show|pending|flush|test|reset.");
            }
        }

        private static CommandResult Status(IDevConsoleContext ctx)
        {
            if (!RequireService<ISurveyService>(ctx, out var survey, out var error)) return error;

            var config = survey.Config;
            string configLabel = config == null
                ? "SIN CONFIG (Rollgeon → Survey → Setup Survey)"
                : $"evento='{config.EventId}' piso={config.TriggerFloorIndex} enabled={config.Enabled} " +
                  $"preguntas={config.Questions?.Count ?? 0} endpoint={(config.HasEndpoint ? "OK" : "vacío → solo disco")}";

            return CommandResult.Ok(
                $"activa: {survey.IsEnabled} | build evento: {survey.IsEventBuild} | {configLabel} | " +
                $"pendientes: {survey.PendingCount} | ya mostrada esta run: {survey.PromptedThisRun}");
        }

        private static CommandResult Show(IDevConsoleContext ctx)
        {
            if (!RequireService<IScreenManager>(ctx, out var screens, out var error)) return error;
            if (screens.Current is SurveyOverlay) return CommandResult.Fail("La encuesta ya está abierta.");

            // No pasa por ShouldPrompt a propósito: es para ver el formulario cuando uno quiera.
            screens.PushOverlay<SurveyOverlay>();
            return CommandResult.Ok("Encuesta abierta (si no aparece, Canvas_Survey no está en la escena).");
        }

        private static CommandResult Pending(IDevConsoleContext ctx)
        {
            if (!RequireService<ISurveyService>(ctx, out var survey, out var error)) return error;

            var keys = survey.PendingKeys;
            if (keys.Count == 0) return CommandResult.Ok("Sin respuestas pendientes.");
            return CommandResult.Ok($"{keys.Count} pendiente(s):\n" + string.Join("\n", keys));
        }

        private static CommandResult Flush(IDevConsoleContext ctx)
        {
            if (!RequireService<ISurveyService>(ctx, out var survey, out var error)) return error;

            int before = survey.PendingCount;
            if (before == 0) return CommandResult.Ok("Nada que enviar.");
            if (survey.Config == null || !survey.Config.HasEndpoint) return CommandResult.Fail("Sin EndpointUrl en SurveyConfig — no hay a dónde mandar.");
            if (!survey.IsEnabled) return CommandResult.Fail("La encuesta está deshabilitada (tick Enabled o build evento).");

            survey.FlushPending();
            return CommandResult.Ok($"Flush lanzado para {before} pendiente(s). Mirá 'survey pending' en unos segundos.");
        }

        private static CommandResult SendTest(IDevConsoleContext ctx)
        {
            if (!RequireService<ISurveyService>(ctx, out var survey, out var error)) return error;

            var response = new SurveyResponse
            {
                locale = "es",
                hero_id = "devconsole",
                run_id = "devconsole",
                answers = new List<SurveyAnswer> { new SurveyAnswer("test", "devconsole") },
            };
            survey.Submit(response);

            return CommandResult.Ok(
                $"Respuesta sintética {response.response_id} guardada" +
                (survey.Config != null && survey.Config.HasEndpoint ? " y enviándose." : " (sin endpoint: queda en disco).") +
                " En la planilla aparece con columna q_test — borrarla a mano.");
        }

        private static CommandResult Reset(IDevConsoleContext ctx)
        {
            if (!RequireService<ISurveyService>(ctx, out var survey, out var error)) return error;

            survey.ResetPromptGuard();
            return CommandResult.Ok("Guard reseteado: vuelve a dispararse al limpiar el piso configurado en esta run.");
        }
    }
}
