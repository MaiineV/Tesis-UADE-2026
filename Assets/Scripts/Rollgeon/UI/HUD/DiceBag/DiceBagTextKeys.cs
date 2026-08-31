using Rollgeon.Upgrades.Dice;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Claves de la tabla UI del panel de la bolsa de dados.
    /// </summary>
    public static class DiceBagTextKeys
    {
        public const string Title = "dicebag.title";

        /// <summary>Dado sin ningún encantamiento aplicado.</summary>
        public const string NoEnchantments = "dicebag.no_enchantments";

        // Labels de categoría del acordeón ("Ancla - Control").
        public const string CatAtaque = "dicebag.cat.ataque";
        public const string CatControl = "dicebag.cat.control";
        public const string CatDefensa = "dicebag.cat.defensa";
        public const string CatEconomia = "dicebag.cat.economia";
        public const string CatMaldicion = "dicebag.cat.maldicion";

        /// <summary>Key del label de una categoría; null para None (la UI omite el segmento).</summary>
        public static string CategoryKey(EnchantmentCategory category) => category switch
        {
            EnchantmentCategory.Ataque => CatAtaque,
            EnchantmentCategory.Control => CatControl,
            EnchantmentCategory.Defensa => CatDefensa,
            EnchantmentCategory.Economia => CatEconomia,
            EnchantmentCategory.Maldicion => CatMaldicion,
            _ => null,
        };
    }
}
