namespace Rollgeon.Items
{
    // God se agrega AL FINAL (append) — item-editor-spec.md §5.1. Los assets guardan
    // el int del enum; insertarlo en el medio correría todos los valores existentes
    // sin un solo error de compilación. NO renombrar Common/Uncommon/Rare/Legendary:
    // aunque el GDD de pasivas usa otro vocabulario (Normal/Raro/Épico/Legendario/Dios),
    // ~25 call sites tienen números pegados a estos nombres (precios, HP de cofre,
    // pesos de loot) — ver RarityPalette.DisplayName para el mapeo de etiquetas.
    public enum ItemRarity { Common, Uncommon, Rare, Legendary, God }
}
