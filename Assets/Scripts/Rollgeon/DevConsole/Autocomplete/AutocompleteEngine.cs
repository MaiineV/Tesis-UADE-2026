using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Commands;
using Rollgeon.DevConsole.Core;
using Rollgeon.DevConsole.Parsing;

namespace Rollgeon.DevConsole.Autocomplete
{
    public enum SuggestionTarget { None, CommandName, ArgValue }

    public sealed class AutocompleteResult
    {
        public SuggestionTarget Target { get; }
        public IReadOnlyList<Suggestion> Suggestions { get; }
        public int ReplaceStart { get; }
        public int ReplaceEnd { get; }

        public AutocompleteResult(SuggestionTarget target, IReadOnlyList<Suggestion> suggestions,
            int replaceStart, int replaceEnd)
        {
            Target = target;
            Suggestions = suggestions;
            ReplaceStart = replaceStart;
            ReplaceEnd = replaceEnd;
        }

        public static readonly AutocompleteResult Empty =
            new AutocompleteResult(SuggestionTarget.None, Array.Empty<Suggestion>(), 0, 0);
    }

    /// <summary>
    /// Motor de autocompletado. Dado el texto y el caret, decide si sugerir nombre de
    /// comando o valor del parámetro activo, mantiene la navegación (↑/↓) y aplica la
    /// sugerencia elegida. Pure C# — testeable sin Unity.
    /// </summary>
    public sealed class AutocompleteEngine
    {
        private readonly DevCommandRegistry _registry;
        private AutocompleteResult _last = AutocompleteResult.Empty;
        private string _lastInput;
        private int _lastCaret = -1;

        public int SelectedIndex { get; private set; }
        public AutocompleteResult Current => _last;
        public bool HasSuggestions => _last.Suggestions.Count > 0;

        public AutocompleteEngine(DevCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public AutocompleteResult Compute(string input, int caret, IDevConsoleContext ctx)
        {
            input ??= string.Empty;
            if (caret < 0) caret = 0;
            if (caret > input.Length) caret = input.Length;

            bool changed = input != _lastInput || caret != _lastCaret;

            var parsed = DevCommandParser.Parse(input, caret);
            SuggestionTarget target;
            IEnumerable<string> candidates;
            int replaceStart, replaceEnd;

            if (parsed.ActiveTokenIndex == 0)
            {
                target = SuggestionTarget.CommandName;
                candidates = _registry.AllNames;
                GetReplaceRange(parsed, caret, out replaceStart, out replaceEnd);
            }
            else if (_registry.TryGet(parsed.CommandToken, out var cmd))
            {
                int argIndex = parsed.ActiveTokenIndex - 1;
                ArgSpec spec = ResolveSpec(cmd, argIndex);
                if (spec?.Options != null)
                {
                    target = SuggestionTarget.ArgValue;
                    candidates = spec.Options.GetOptions(ctx);
                }
                else
                {
                    target = SuggestionTarget.None;
                    candidates = Array.Empty<string>();
                }
                GetReplaceRange(parsed, caret, out replaceStart, out replaceEnd);
            }
            else
            {
                target = SuggestionTarget.None;
                candidates = Array.Empty<string>();
                replaceStart = replaceEnd = caret;
            }

            var filtered = Filter(candidates, parsed.ActiveTokenPrefix ?? string.Empty);
            _last = new AutocompleteResult(target, filtered, replaceStart, replaceEnd);

            if (changed) SelectedIndex = 0;
            if (filtered.Count == 0) SelectedIndex = 0;
            else if (SelectedIndex > filtered.Count - 1) SelectedIndex = filtered.Count - 1;
            else if (SelectedIndex < 0) SelectedIndex = 0;

            _lastInput = input;
            _lastCaret = caret;
            return _last;
        }

        public void MoveDown()
        {
            int count = _last.Suggestions.Count;
            if (count == 0) return;
            SelectedIndex = (SelectedIndex + 1) % count;
        }

        public void MoveUp()
        {
            int count = _last.Suggestions.Count;
            if (count == 0) return;
            SelectedIndex = (SelectedIndex - 1 + count) % count;
        }

        public void Reset()
        {
            _last = AutocompleteResult.Empty;
            SelectedIndex = 0;
            _lastInput = null;
            _lastCaret = -1;
        }

        /// <summary>Aplica la sugerencia seleccionada al último input computado.</summary>
        public bool TryAccept(out string newInput, out int newCaret)
        {
            newInput = _lastInput ?? string.Empty;
            newCaret = newInput.Length;
            if (_last.Suggestions.Count == 0) return false;

            int sel = SelectedIndex;
            if (sel < 0) sel = 0;
            if (sel > _last.Suggestions.Count - 1) sel = _last.Suggestions.Count - 1;

            string insert = QuoteIfNeeded(_last.Suggestions[sel].Text);
            int start = Clamp(_last.ReplaceStart, 0, newInput.Length);
            int end = Clamp(_last.ReplaceEnd, start, newInput.Length);

            newInput = newInput.Substring(0, start) + insert + newInput.Substring(end);
            newCaret = start + insert.Length;

            // Espacio para empezar a tipear el siguiente arg, si no hay uno ya.
            if (newCaret >= newInput.Length || newInput[newCaret] != ' ')
            {
                newInput = newInput.Insert(newCaret, " ");
                newCaret += 1;
            }
            return true;
        }

        private static ArgSpec ResolveSpec(IDevCommand cmd, int argIndex)
        {
            var args = cmd.Args;
            if (args == null || args.Count == 0 || argIndex < 0) return null;
            // Variádicos (p.ej. setdiceroll): clamp al último spec.
            return argIndex < args.Count ? args[argIndex] : args[args.Count - 1];
        }

        private static void GetReplaceRange(ParsedCommandLine parsed, int caret, out int start, out int end)
        {
            if (!parsed.CaretInNewToken && parsed.ActiveTokenIndex < parsed.Tokens.Count)
            {
                var tok = parsed.Tokens[parsed.ActiveTokenIndex];
                start = tok.Start;
                end = tok.End;
            }
            else
            {
                start = end = caret;
            }
        }

        private static IReadOnlyList<Suggestion> Filter(IEnumerable<string> candidates, string prefix)
        {
            var starts = new List<string>();
            var contains = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (candidates != null)
            {
                foreach (var c in candidates)
                {
                    if (string.IsNullOrEmpty(c) || !seen.Add(c)) continue;
                    if (prefix.Length == 0 || c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        starts.Add(c);
                    else if (c.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        contains.Add(c);
                }
            }

            starts.Sort(StringComparer.OrdinalIgnoreCase);
            contains.Sort(StringComparer.OrdinalIgnoreCase);

            var result = new List<Suggestion>(starts.Count + contains.Count);
            foreach (var s in starts) result.Add(new Suggestion(s));
            foreach (var s in contains) result.Add(new Suggestion(s));
            return result;
        }

        private static string QuoteIfNeeded(string s)
            => string.IsNullOrEmpty(s) ? s : (s.IndexOf(' ') >= 0 ? "\"" + s + "\"" : s);

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
