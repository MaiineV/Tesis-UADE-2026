using System.Collections.Generic;
using Rollgeon.DevConsole.Autocomplete;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Commands;
using Rollgeon.DevConsole.Core;
using Rollgeon.DevConsole.Parsing;

namespace Rollgeon.DevConsole.UI
{
    /// <summary>
    /// Estado lógico de la consola (sin Unity UI): contexto, registry, autocompletado, controllers
    /// de cheat, log y ejecución. La capa MonoBehaviour la maneja y renderiza.
    /// </summary>
    public sealed class DevConsoleSession
    {
        public BufferLogSink Log { get; }
        public IDevConsoleContext Ctx { get; }
        public DevCommandRegistry Registry { get; }
        public AutocompleteEngine Autocomplete { get; }
        public GodModeController God { get; }
        public InfiniteEnergyController Energy { get; }
        public FreeMoveController FreeMove { get; }

        private readonly List<string> _history = new List<string>();

        public DevConsoleSession()
        {
            Log = new BufferLogSink();
            Ctx = new ServiceLocatorConsoleContext(Log);
            God = new GodModeController(Ctx);
            Energy = new InfiniteEnergyController(Ctx);
            FreeMove = new FreeMoveController();
            Registry = DefaultCommands.CreateDefault(Ctx, God, Energy, FreeMove);
            Autocomplete = new AutocompleteEngine(Registry);

            Log.Info("DevConsole lista. Escribí 'help' para ver los comandos.");
        }

        public IReadOnlyList<string> History => _history;

        public void Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            Log.Info("> " + line);
            _history.Add(line);

            var parsed = DevCommandParser.Parse(line, line.Length);
            if (string.IsNullOrWhiteSpace(parsed.CommandToken))
            {
                Log.Error("Comando vacío.");
                return;
            }
            if (!Registry.TryGet(parsed.CommandToken, out var cmd))
            {
                Log.Error($"Comando desconocido: '{parsed.CommandToken}'. Probá 'help'.");
                return;
            }

            var result = cmd.Execute(parsed.Args, Ctx);
            if (!string.IsNullOrEmpty(result.Message))
            {
                if (result.Success) Log.Info(result.Message);
                else Log.Error(result.Message);
            }
        }
    }
}
