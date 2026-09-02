namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Categoría de un encantamiento — el "tipo" que la UI muestra junto al nombre
    /// (drawer de la bolsa: "Ancla - Control"). Es clasificación de lectura para el
    /// jugador, no afecta el runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>None</c> = asset sin clasificar: el test de auditoría lo rechaza, y la UI
    /// omite el segmento del tipo. Poblar con el MenuItem
    /// <c>Rollgeon → Enchantments → Assign Categories</c>.
    /// </para>
    /// <para>
    /// APPEND-ONLY: los assets de Odin serializan el int, renumerar corrompe la
    /// clasificación en silencio. Taxonomía vigente (GDD "Listado encantamientos",
    /// 2026-09): Caos / Recursos / Ataque / Control / Movimiento. Los miembros
    /// <c>Defensa</c>, <c>Economia</c> y <c>Maldicion</c> son legacy — no autorar
    /// contenido nuevo con ellos (Defensa/Economia → Recursos, Maldicion → Caos).
    /// </para>
    /// </remarks>
    public enum EnchantmentCategory
    {
        None = 0,
        Ataque,
        Control,
        Defensa,
        Economia,
        Maldicion,
        Caos,
        Recursos,
        Movimiento,
    }
}
