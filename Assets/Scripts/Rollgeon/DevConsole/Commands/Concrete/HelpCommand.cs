using System.Collections.Generic;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class HelpCommand : DevCommandBase
    {
        private readonly DevCommandRegistry _registry;
        private static readonly ArgSpec[] _args = { new ArgSpec("cmd", ArgKind.String, optional: true) };
        private static readonly string[] _aliases = { "?" };

        public HelpCommand(DevCommandRegistry registry) => _registry = registry;

        public override string Name => "help";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "Lista los comandos o describe uno.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args.Count > 0)
            {
                if (!_registry.TryGet(args[0], out var cmd))
                    return CommandResult.Fail($"Comando desconocido: '{args[0]}'.");

                ctx.Log.Info($"{cmd.Name} — {cmd.Description}");
                if (cmd.Args != null && cmd.Args.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var a in cmd.Args) parts.Add(a.Optional ? $"[{a.Name}]" : $"<{a.Name}>");
                    ctx.Log.Info($"  uso: {cmd.Name} {string.Join(" ", parts)}");
                }
                if (cmd.Aliases != null && cmd.Aliases.Count > 0)
                    ctx.Log.Info($"  alias: {string.Join(", ", cmd.Aliases)}");
                return CommandResult.Ok();
            }

            ctx.Log.Info("Comandos disponibles (usá 'help <cmd>' para detalle):");
            foreach (var c in _registry.All) ctx.Log.Info($"  {c.Name} — {c.Description}");
            return CommandResult.Ok();
        }
    }
}
