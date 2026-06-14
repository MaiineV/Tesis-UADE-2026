namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Operación que un trigger genérico aplica sobre un recurso (oro / stat).
    /// Orden de resolución fijo: (base + Σ Add/Subtract) × Π Multiply, y <see cref="Set"/>
    /// pisa la base. Así el resultado es determinista sin importar el orden de dispatch
    /// entre encantamientos (suma conmuta; multiplicación se compone después).
    /// </summary>
    public enum ResourceOperation
    {
        Add,
        Subtract,
        Multiply,
        Set,
    }
}
