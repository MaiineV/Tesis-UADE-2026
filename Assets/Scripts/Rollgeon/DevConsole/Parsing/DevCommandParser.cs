using System.Collections.Generic;

namespace Rollgeon.DevConsole.Parsing
{
    /// <summary>
    /// Tokeniza la línea respetando comillas dobles y resuelve qué token está bajo el
    /// caret (para autocompletado). Pure C#, sin dependencias de Unity.
    /// </summary>
    public static class DevCommandParser
    {
        public static ParsedCommandLine Parse(string input, int caret)
        {
            input ??= string.Empty;
            int n = input.Length;
            if (caret < 0) caret = 0;
            if (caret > n) caret = n;

            // Descartar un único '/' inicial (el prompt), respetando espacios previos.
            int scan = 0;
            int firstNonSpace = 0;
            while (firstNonSpace < n && input[firstNonSpace] == ' ') firstNonSpace++;
            if (firstNonSpace < n && input[firstNonSpace] == '/') scan = firstNonSpace + 1;

            var tokens = new List<ParsedToken>();
            int i = scan;
            while (i < n)
            {
                while (i < n && input[i] == ' ') i++;
                if (i >= n) break;

                int start = i;
                if (input[i] == '"')
                {
                    i++; // comilla de apertura
                    int contentStart = i;
                    while (i < n && input[i] != '"') i++;
                    string text = input.Substring(contentStart, i - contentStart);
                    if (i < n && input[i] == '"') i++; // comilla de cierre (si existe)
                    tokens.Add(new ParsedToken(text, start, i, quoted: true));
                }
                else
                {
                    while (i < n && input[i] != ' ') i++;
                    string text = input.Substring(start, i - start);
                    tokens.Add(new ParsedToken(text, start, i, quoted: false));
                }
            }

            // Resolver el token activo según la posición del caret.
            int activeIndex = tokens.Count;
            string activePrefix = string.Empty;
            bool caretInNewToken = true;
            for (int t = 0; t < tokens.Count; t++)
            {
                var tok = tokens[t];
                if (caret >= tok.Start && caret <= tok.End)
                {
                    activeIndex = t;
                    int contentStart = tok.Quoted ? tok.Start + 1 : tok.Start;
                    int len = caret - contentStart;
                    if (len < 0) len = 0;
                    if (len > tok.Text.Length) len = tok.Text.Length;
                    activePrefix = tok.Text.Substring(0, len);
                    caretInNewToken = false;
                    break;
                }
                if (caret < tok.Start)
                {
                    activeIndex = t;
                    activePrefix = string.Empty;
                    caretInNewToken = true;
                    break;
                }
            }

            string commandToken = tokens.Count > 0 ? tokens[0].Text : string.Empty;
            var args = new List<string>(tokens.Count > 0 ? tokens.Count - 1 : 0);
            for (int a = 1; a < tokens.Count; a++) args.Add(tokens[a].Text);

            return new ParsedCommandLine(commandToken, args, tokens, activeIndex, activePrefix, caretInNewToken);
        }
    }
}
