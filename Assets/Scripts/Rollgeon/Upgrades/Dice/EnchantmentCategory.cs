namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Categoría de un encantamiento — el "tipo" que la UI muestra junto al nombre
    /// (drawer de la bolsa: "Ancla - Control"). Es clasificación de lectura para el
    /// jugador, no afecta el runtime.
    /// </summary>
    /// <remarks>
    /// <c>None</c> = asset sin clasificar: el test de auditoría lo rechaza, y la UI
    /// omite el segmento del tipo. Poblar con el MenuItem
    /// <c>Rollgeon → Enchantments → Assign Categories</c>.
    /// </remarks>
    public enum EnchantmentCategory
    {
        None = 0,
        Ataque,
        Control,
        Defensa,
        Economia,
        Maldicion,
    }
}
