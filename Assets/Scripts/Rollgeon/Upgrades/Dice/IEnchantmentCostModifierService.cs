namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Multiplicadores del costo del altar de encantamiento aportados por items
    /// (Moneda Maldita: ×0.5). Se componen multiplicativamente entre fuentes; el
    /// costo final nunca baja de 1 (GDD: "el descuento no baja el costo del mínimo").
    /// </summary>
    /// <remarks>
    /// La otra mitad de Moneda Maldita ("mayor probabilidad de encantamiento caos")
    /// vive en <see cref="IEnchantmentWeightModifierService"/>: pondera los
    /// encantamientos malditos (CapCursed / categoría Maldición) del pool del altar.
    /// </remarks>
    public interface IEnchantmentCostModifierService
    {
        /// <summary>Registra (o reemplaza) el multiplicador de <paramref name="sourceId"/> (ItemId).</summary>
        void Register(string sourceId, float costMultiplier);

        void Unregister(string sourceId);

        /// <summary>Producto de todos los multiplicadores registrados. 1 sin fuentes.</summary>
        float ResolveMultiplier();
    }
}
