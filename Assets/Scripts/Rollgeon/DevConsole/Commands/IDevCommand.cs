using System.Collections.Generic;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Un comando de la consola. Toda la lógica de cheats se rutea por estos.</summary>
    public interface IDevCommand
    {
        string Name { get; }
        IReadOnlyList<string> Aliases { get; }
        string Description { get; }
        IReadOnlyList<ArgSpec> Args { get; }
        CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx);
    }
}
