using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Paleta de títulos de encantamientos (mismo patrón que
    /// <c>Rollgeon.Items.RarityPalette</c>): maldiciones en rojo, bendiciones en
    /// dorado. Fuente de verdad: Rollgeon_Paleta_de_Color.md. Si aparece otro
    /// consumidor (shop, tooltips nuevos), leer de acá y no duplicar hexas.
    /// </summary>
    public static class EnchantmentPalette
    {
        /// <summary>#D1365A — títulos de maldiciones.</summary>
        public static readonly Color32 Cursed = new Color32(0xD1, 0x36, 0x5A, 0xFF);

        /// <summary>#D9A44E — títulos de encantamientos no malditos.</summary>
        public static readonly Color32 Blessed = new Color32(0xD9, 0xA4, 0x4E, 0xFF);

        public static Color32 TitleColor(EnchantmentSO enchantment)
            => enchantment.IsCursed() ? Cursed : Blessed;

        /// <summary>Hex RRGGBB (sin #) para rich text inline.</summary>
        public static string TitleHex(EnchantmentSO enchantment)
            => ColorUtility.ToHtmlStringRGB(TitleColor(enchantment));

        // ------------------------------------------------------------------
        // Colores por categoría — regla 9.1 de la paleta: reusar hexas
        // existentes en vez de inventar (telegraph, maná, escudo, oro, vida).
        // ------------------------------------------------------------------

        /// <summary>#E0763D — Ataque (naranja telegraph).</summary>
        public static readonly Color32 Ataque = new Color32(0xE0, 0x76, 0x3D, 0xFF);

        /// <summary>#6E7FD1 — Control (azul maná).</summary>
        public static readonly Color32 Control = new Color32(0x6E, 0x7F, 0xD1, 0xFF);

        /// <summary>#A3B3B1 — Defensa (gris escudo).</summary>
        public static readonly Color32 Defensa = new Color32(0xA3, 0xB3, 0xB1, 0xFF);

        /// <summary>#D9A44E — Economía (dorado oro).</summary>
        public static readonly Color32 Economia = new Color32(0xD9, 0xA4, 0x4E, 0xFF);

        /// <summary>#D1365A — Maldición (rojo vida, mismo hex que Cursed).</summary>
        public static readonly Color32 Maldicion = new Color32(0xD1, 0x36, 0x5A, 0xFF);

        /// <summary>#D1365A — Caos (taxonomía GDD; hereda el rojo de Maldición/Cursed).</summary>
        public static readonly Color32 Caos = new Color32(0xD1, 0x36, 0x5A, 0xFF);

        /// <summary>#D9A44E — Recursos (taxonomía GDD; hereda el dorado de Economía/oro).</summary>
        public static readonly Color32 Recursos = new Color32(0xD9, 0xA4, 0x4E, 0xFF);

        /// <summary>#63E063 — Movimiento (verde curación, FloatingNumberPalette.Heal).</summary>
        public static readonly Color32 Movimiento = new Color32(0x63, 0xE0, 0x63, 0xFF);

        public static Color32 CategoryColor(EnchantmentCategory category) => category switch
        {
            EnchantmentCategory.Ataque => Ataque,
            EnchantmentCategory.Control => Control,
            EnchantmentCategory.Defensa => Defensa,
            EnchantmentCategory.Economia => Economia,
            EnchantmentCategory.Maldicion => Maldicion,
            EnchantmentCategory.Caos => Caos,
            EnchantmentCategory.Recursos => Recursos,
            EnchantmentCategory.Movimiento => Movimiento,
            _ => Blessed,
        };

        /// <summary>Hex RRGGBB (sin #) del color de la categoría, para rich text inline.</summary>
        public static string CategoryHex(EnchantmentCategory category)
            => ColorUtility.ToHtmlStringRGB(CategoryColor(category));
    }
}
