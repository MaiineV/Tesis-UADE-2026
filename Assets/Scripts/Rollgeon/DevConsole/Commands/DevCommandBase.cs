using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Base con helpers de parseo/validación para no repetirlos en cada comando.</summary>
    public abstract class DevCommandBase : IDevCommand
    {
        private static readonly string[] NoAliases = Array.Empty<string>();
        private static readonly ArgSpec[] NoArgs = Array.Empty<ArgSpec>();

        public abstract string Name { get; }
        public virtual IReadOnlyList<string> Aliases => NoAliases;
        public abstract string Description { get; }
        public virtual IReadOnlyList<ArgSpec> Args => NoArgs;

        public abstract CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx);

        protected static bool TryInt(IReadOnlyList<string> args, int index, out int value)
        {
            value = 0;
            return args != null && index >= 0 && index < args.Count
                   && int.TryParse(args[index], out value);
        }

        protected static bool TryEnum<TEnum>(string s, out TEnum value) where TEnum : struct
            => Enum.TryParse(s, ignoreCase: true, out value) && Enum.IsDefined(typeof(TEnum), value);

        protected static bool RequireRun(IDevConsoleContext ctx, out CommandResult error)
        {
            if (!ctx.IsRunActive) { error = CommandResult.Fail("No hay una run activa."); return false; }
            error = default;
            return true;
        }

        protected static bool RequirePlayer(IDevConsoleContext ctx, out Guid playerGuid, out CommandResult error)
        {
            playerGuid = ctx.PlayerGuid;
            if (playerGuid == Guid.Empty) { error = CommandResult.Fail("No hay player activo."); return false; }
            error = default;
            return true;
        }

        protected static bool RequireService<T>(IDevConsoleContext ctx, out T service, out CommandResult error)
        {
            if (!ctx.TryResolve(out service) || service == null)
            {
                error = CommandResult.Fail($"Servicio no disponible: {typeof(T).Name}");
                return false;
            }
            error = default;
            return true;
        }
    }
}
