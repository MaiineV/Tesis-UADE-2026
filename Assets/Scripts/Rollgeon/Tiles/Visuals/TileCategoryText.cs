using Rollgeon.Localization;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// La fila de tipo del panel de una casilla — <c>Casilla · Daño</c> — el equivalente del
    /// <c>Jefe · Rango</c> de un enemigo (<c>EnemyArchetypeText</c>).
    /// </summary>
    public static class TileCategoryText
    {
        public const string FormatKey = "tile.category.format";

        /// <summary>
        /// Cadena vacía = el panel no dibuja la fila. Sólo pasa con una categoría reservada del
        /// GDD que todavía no tiene texto: nombrarla acá antes de que exista sería inventarla.
        /// </summary>
        public static string Describe(TileEffectCategory category)
        {
            string key = KeyFor(category);
            if (key == null) return string.Empty;

            return LocalizedContent.FromTableFormat(
                LocalizedContent.UITable, FormatKey, "Casilla · {0}",
                LocalizedContent.Ui(key, FallbackFor(category)));
        }

        public static string KeyFor(TileEffectCategory category) => category switch
        {
            TileEffectCategory.Damage => "tile.category.damage",
            TileEffectCategory.Heal => "tile.category.heal",
            TileEffectCategory.ApplyStatus => "tile.category.status",
            TileEffectCategory.StatModifier => "tile.category.buff",
            TileEffectCategory.MoveRangeBonus => "tile.category.buff",
            TileEffectCategory.ForcedSlide => "tile.category.slide",
            TileEffectCategory.Teleport => "tile.category.teleport",
            TileEffectCategory.Telegraph => "tile.category.warning",
            TileEffectCategory.ConditionalProtection => "tile.category.protection",
            _ => null,
        };

        private static string FallbackFor(TileEffectCategory category) => category switch
        {
            TileEffectCategory.Damage => "Daño",
            TileEffectCategory.Heal => "Curación",
            TileEffectCategory.ApplyStatus => "Estado",
            TileEffectCategory.StatModifier => "Mejora",
            TileEffectCategory.MoveRangeBonus => "Mejora",
            TileEffectCategory.ForcedSlide => "Deslizamiento",
            TileEffectCategory.Teleport => "Teletransporte",
            TileEffectCategory.Telegraph => "Advertencia",
            TileEffectCategory.ConditionalProtection => "Protección",
            _ => string.Empty,
        };
    }
}
