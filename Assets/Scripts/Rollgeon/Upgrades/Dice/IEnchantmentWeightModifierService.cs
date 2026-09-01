namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Multiplicadores del peso de los encantamientos malditos (CapCursed / categoría
    /// Maldición) en el pool del altar, aportados por items (Moneda Maldita: ×3).
    /// Se componen multiplicativamente entre fuentes. Es la otra mitad de Moneda
    /// Maldita que <see cref="IEnchantmentCostModifierService"/> dejaba pendiente.
    /// </summary>
    public interface IEnchantmentWeightModifierService
    {
        /// <summary>Registra (o reemplaza) el multiplicador de <paramref name="sourceId"/> (ItemId).</summary>
        void Register(string sourceId, float cursedWeightMultiplier);

        void Unregister(string sourceId);

        /// <summary>Producto de todos los multiplicadores registrados. 1 sin fuentes.</summary>
        float ResolveCursedMultiplier();
    }
}
