using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Meta;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Prende/apaga el tutorial (auto-launch de la primera run). 'tutorial off' además
    /// lo marca como completado, así tampoco lo dispara el botón "Tutorial" del menú.
    /// </summary>
    public sealed class TutorialCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("on|off", ArgKind.Choice, optional: true, ArgProviders.OnOff)
        };

        public override string Name => "tutorial";
        public override string Description =>
            "Prende/apaga el tutorial. 'tutorial' togglea, 'tutorial on'/'tutorial off'. Off también lo marca completado.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IMetaProgressionService>(ctx, out var meta, out var err)) return err;

            bool enable;
            if (args.Count == 0) enable = !meta.IsTutorialEnabled;
            else if (string.Equals(args[0], "on", StringComparison.OrdinalIgnoreCase)) enable = true;
            else if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)) enable = false;
            else return CommandResult.Fail("Usá 'tutorial', 'tutorial on' o 'tutorial off'.");

            meta.SetTutorialEnabled(enable);
            // Off = no lo quiero ni por auto-launch ni por el botón: marcarlo completado cierra ambos gates.
            if (!enable) meta.MarkTutorialCompleted();
            meta.SaveNow();

            return CommandResult.Ok($"Tutorial: {(enable ? "ON" : "OFF")}."
                + (enable ? string.Empty : " (marcado completado — Jugar va directo a selección de clase)"));
        }
    }
}
