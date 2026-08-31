using System.Globalization;
using System.Text;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Derives an <c>ItemSO.ItemId</c> from a Display Name (item-editor-spec.md §3): lowercase, no
    /// accents, dot-separated. <c>"Banquete Real"</c> → <c>"banquete.real"</c>.
    /// </summary>
    /// <remarks>
    /// Ids already on disk (e.g. <c>bendicion.destinoo.generala</c> for "Bendicion del Destino") do
    /// NOT all reproduce this exact algorithm — they were hand-authored before this tool existed and
    /// the id is frozen at creation (spec §3 rule 2: renaming the Display Name later does not touch
    /// it). This derivation only has to hold for ids created going forward.
    /// </remarks>
    public static class ItemIdSlug
    {
        /// <summary>Empty/whitespace input returns an empty string — callers treat that as invalid.</summary>
        public static string FromDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;

            var withoutAccents = StripAccents(displayName);

            var sb = new StringBuilder(withoutAccents.Length);
            bool lastWasSeparator = true; // swallow a leading separator
            foreach (var c in withoutAccents)
            {
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    sb.Append('.');
                    lastWasSeparator = true;
                }
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == '.')
                sb.Length -= 1;

            return sb.ToString();
        }

        /// <summary>
        /// Unicode NFD decomposition + drop combining marks. "á" decomposes to "a" + a combining
        /// acute accent (Mn category), which the loop below strips — leaving plain "a". Same trick
        /// turns "ñ" into "n" + combining tilde → "n".
        /// </summary>
        static string StripAccents(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
