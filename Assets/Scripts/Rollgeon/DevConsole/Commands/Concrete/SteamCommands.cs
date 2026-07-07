using System.Collections.Generic;
using System.Text;
using Rollgeon.Achievements;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Diagnóstico y prueba de la integración Steam (Feature#0019):
    /// <c>steam status</c> / <c>steam ach list|unlock|clear &lt;key&gt;</c>.
    /// Habla solo con <see cref="ISteamService"/> y <see cref="AchievementService"/> —
    /// este assembly no referencia Steamworks.
    /// </summary>
    public sealed class SteamCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("status|ach", ArgKind.String),
            new ArgSpec("list|unlock|clear", ArgKind.String, optional: true),
            new ArgSpec("key", ArgKind.String, optional: true),
        };

        public override string Name => "steam";
        public override string Description => "Estado de Steam y logros: steam status | steam ach list|unlock|clear <key>.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args == null || args.Count == 0)
            {
                return CommandResult.Fail("Usá 'steam status' o 'steam ach list|unlock|clear <key>'.");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status": return Status(ctx);
                case "ach": return Achievements(args, ctx);
                default: return CommandResult.Fail($"Subcomando desconocido '{args[0]}'. Usá status|ach.");
            }
        }

        private static CommandResult Status(IDevConsoleContext ctx)
        {
            if (!RequireService<ISteamService>(ctx, out var steam, out var error)) return error;

            var appId = ctx.TryResolve<SteamConfigSO>(out var config) && config != null
                ? config.AppId.ToString()
                : "sin SteamConfig";

            return CommandResult.Ok(steam.Available
                ? $"Steam OK — usuario '{steam.PlayerName}', AppId {appId}."
                : $"Steam no disponible (AppId {appId}).");
        }

        private static CommandResult Achievements(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<AchievementService>(ctx, out var ach, out var error)) return error;

            var action = args.Count > 1 ? args[1].ToLowerInvariant() : "list";
            switch (action)
            {
                case "list":
                {
                    var sb = new StringBuilder("Logros:");
                    var any = false;
                    foreach (var (def, unlocked) in ach.ListWithStatus())
                    {
                        any = true;
                        var state = unlocked switch
                        {
                            true => "desbloqueado",
                            false => "bloqueado",
                            null => "estado desconocido (sin Steam)",
                        };
                        sb.Append($"\n  {def.Key} → {def.SteamApiName} [{def.Trigger}] — {state}");
                    }

                    return CommandResult.Ok(any ? sb.ToString() : "Sin logros en el SteamConfig.");
                }
                case "unlock":
                    if (args.Count < 3) return CommandResult.Fail("Usá 'steam ach unlock <key>'.");
                    return ach.TryUnlockByKey(args[2])
                        ? CommandResult.Ok($"Logro '{args[2]}' desbloqueado.")
                        : CommandResult.Fail(
                            $"No se pudo desbloquear '{args[2]}' — ¿key inexistente, Steam ausente, " +
                            "logro sin publicar en el partner site, o ya desbloqueado?");
                case "clear":
                    if (args.Count < 3) return CommandResult.Fail("Usá 'steam ach clear <key>'.");
                    return ach.TryClearByKey(args[2])
                        ? CommandResult.Ok($"Logro '{args[2]}' reseteado.")
                        : CommandResult.Fail($"No se pudo resetear '{args[2]}' — ¿key inexistente o Steam ausente?");
                default:
                    return CommandResult.Fail($"Acción desconocida '{args[1]}'. Usá list|unlock|clear.");
            }
        }
    }
}
