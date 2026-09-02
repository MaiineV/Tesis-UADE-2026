using System.Globalization;
using System.Text;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Deriva un <c>UpgradeSO.UpgradeId</c> de canal dados desde un Display Name:
    /// prefijo de canal + snake_case sin acentos. <c>"Múltiplo de 3"</c> →
    /// <c>"ench.multiplo_de_3"</c>. Espejo de <c>ItemIdSlug</c>, con la convención
    /// <c>'&lt;channel&gt;.&lt;snake_case&gt;'</c> de <c>UpgradeSO</c> en vez del dot-separated
    /// de items.
    /// </summary>
    /// <remarks>
    /// Los 33 ids ya en disco siguen esta forma (<c>ench.caras_centrales</c>,
    /// <c>ench.multiplo_de_3</c>) pero no todos reproducen la derivación exacta
    /// (<c>ench.only_evens</c>, <c>ench.gold_on_roll</c> se autoraron en inglés a mano).
    /// El id se congela al crear — esta derivación solo rige para ids nuevos.
    /// </remarks>
    public static class EnchantmentIdSlug
    {
        /// <summary>Prefijo del canal dados. <c>UpgradeChannel.Dice</c> → <c>"ench."</c>.</summary>
        public const string Prefix = "ench.";

        /// <summary>Vacío/whitespace devuelve string vacío — el caller lo trata como inválido.</summary>
        public static string FromDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;

            var withoutAccents = StripAccents(displayName);

            var sb = new StringBuilder(withoutAccents.Length);
            bool lastWasSeparator = true; // se traga separadores al inicio
            foreach (var c in withoutAccents)
            {
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    sb.Append('_');
                    lastWasSeparator = true;
                }
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == '_')
                sb.Length -= 1;

            return sb.Length == 0 ? string.Empty : Prefix + sb;
        }

        /// <summary>
        /// Descomposición Unicode NFD + descarte de combining marks: "á" → "a", "ñ" → "n".
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
