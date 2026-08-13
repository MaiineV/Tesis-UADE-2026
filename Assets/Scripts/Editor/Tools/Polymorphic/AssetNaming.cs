using System.Text;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>Derives asset file names from authored ids, following the naming already on disk.</summary>
    public static class AssetNaming
    {
        /// <summary>
        /// `potion.healing` → `PotionHealing`, `multiplo_de_3` → `MultiploDe3`. Segment separators
        /// (<c>. _ -</c> and spaces) start a new capitalised word.
        /// </summary>
        /// <remarks>
        /// Verified against the project: with the <c>ench.</c> channel prefix stripped, this
        /// reproduces the file name of all 33 authored enchantments exactly.
        /// </remarks>
        public static string PascalCaseId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            var sb = new StringBuilder(id.Length);
            bool upperNext = true;
            foreach (char c in id)
            {
                if (c == '.' || c == '_' || c == '-' || c == ' ')
                {
                    upperNext = true;
                    continue;
                }
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            return sb.ToString();
        }
    }
}
