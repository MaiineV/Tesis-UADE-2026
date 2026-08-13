namespace Rollgeon.DevConsole.Commands
{
    public enum ArgKind { Int, String, Enum, Choice }

    /// <summary>Describe un parámetro de un comando: para el help y para el autocompletado.</summary>
    public sealed class ArgSpec
    {
        public string Name { get; }
        public ArgKind Kind { get; }
        public bool Optional { get; }

        /// <summary>Provider de opciones para autocompletar. Null = valor libre (sin sugerencias).</summary>
        public IArgProvider Options { get; }

        public ArgSpec(string name, ArgKind kind, bool optional = false, IArgProvider options = null)
        {
            Name = name;
            Kind = kind;
            Optional = optional;
            Options = options;
        }
    }
}
