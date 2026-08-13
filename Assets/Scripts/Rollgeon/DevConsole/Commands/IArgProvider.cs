using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Enumera las opciones válidas de un argumento para el autocompletado.</summary>
    public interface IArgProvider
    {
        IEnumerable<string> GetOptions(IDevConsoleContext ctx);
    }

    /// <summary>Opciones fijas (enums, listas hardcodeadas).</summary>
    public sealed class StaticArgProvider : IArgProvider
    {
        private readonly string[] _options;
        public StaticArgProvider(params string[] options) => _options = options ?? Array.Empty<string>();
        public IEnumerable<string> GetOptions(IDevConsoleContext ctx) => _options;
    }

    /// <summary>Opciones derivadas de servicios vivos (catálogos, inventario, etc.).</summary>
    public sealed class FuncArgProvider : IArgProvider
    {
        private readonly Func<IDevConsoleContext, IEnumerable<string>> _fn;
        public FuncArgProvider(Func<IDevConsoleContext, IEnumerable<string>> fn) => _fn = fn;

        public IEnumerable<string> GetOptions(IDevConsoleContext ctx)
            => _fn != null ? (_fn(ctx) ?? Array.Empty<string>()) : Array.Empty<string>();
    }
}
