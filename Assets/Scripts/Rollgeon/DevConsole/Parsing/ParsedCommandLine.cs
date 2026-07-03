using System.Collections.Generic;

namespace Rollgeon.DevConsole.Parsing
{
    /// <summary>Un token de la línea con sus offsets en el string original.</summary>
    public readonly struct ParsedToken
    {
        public string Text { get; }
        public int Start { get; }
        public int End { get; }   // exclusivo
        public bool Quoted { get; }

        public ParsedToken(string text, int start, int end, bool quoted)
        {
            Text = text;
            Start = start;
            End = end;
            Quoted = quoted;
        }
    }

    /// <summary>Resultado de parsear la línea: comando + args + token activo bajo el caret.</summary>
    public sealed class ParsedCommandLine
    {
        public string CommandToken { get; }
        public IReadOnlyList<string> Args { get; }
        public IReadOnlyList<ParsedToken> Tokens { get; }

        /// <summary>0 = comando; 1..n = arg; Tokens.Count = token nuevo al final.</summary>
        public int ActiveTokenIndex { get; }
        public string ActiveTokenPrefix { get; }
        public bool CaretInNewToken { get; }

        public ParsedCommandLine(string commandToken, IReadOnlyList<string> args,
            IReadOnlyList<ParsedToken> tokens, int activeTokenIndex, string activeTokenPrefix,
            bool caretInNewToken)
        {
            CommandToken = commandToken;
            Args = args;
            Tokens = tokens;
            ActiveTokenIndex = activeTokenIndex;
            ActiveTokenPrefix = activeTokenPrefix;
            CaretInNewToken = caretInNewToken;
        }
    }
}
