using System;
using System.Collections.Generic;
using System.Linq;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Registro de comandos: lookup por nombre/alias (case-insensitive) + enumeración.</summary>
    public sealed class DevCommandRegistry
    {
        private readonly Dictionary<string, IDevCommand> _byKey =
            new Dictionary<string, IDevCommand>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDevCommand> _all = new List<IDevCommand>();

        public void Register(IDevCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            AddKey(command.Name, command);
            if (command.Aliases != null)
                foreach (var alias in command.Aliases) AddKey(alias, command);

            _all.Add(command);
            _all.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        private void AddKey(string key, IDevCommand command)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Command key vacío.");
            if (_byKey.ContainsKey(key))
                throw new ArgumentException($"Comando/alias duplicado: '{key}'.");
            _byKey[key] = command;
        }

        public bool TryGet(string nameOrAlias, out IDevCommand command)
        {
            command = null;
            return !string.IsNullOrWhiteSpace(nameOrAlias) && _byKey.TryGetValue(nameOrAlias, out command);
        }

        /// <summary>Comandos ordenados por nombre (para help y sugerencias).</summary>
        public IReadOnlyList<IDevCommand> All => _all;

        /// <summary>Solo nombres canónicos (lo que se sugiere al autocompletar el comando).</summary>
        public IEnumerable<string> AllNames => _all.Select(c => c.Name);

        /// <summary>Nombres + aliases (para resolución).</summary>
        public IEnumerable<string> AllNamesAndAliases => _byKey.Keys;
    }
}
