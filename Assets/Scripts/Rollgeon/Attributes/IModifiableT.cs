namespace Rollgeon.Attributes
{
    /// <summary>
    /// Conveniencia tipada de <see cref="IModifiable"/>. Los stats concretos
    /// heredan de aca para tener accessor sin boxing tanto del valor crudo
    /// (<see cref="IAttribute{TValue}.Value"/>) como del valor modificado
    /// (<see cref="ModifiedValue"/>).
    /// </summary>
    /// <typeparam name="TValue">Tipo primitivo del valor.</typeparam>
    public interface IModifiable<TValue> : IModifiable, IAttribute<TValue>
    {
        /// <summary>Valor con modificadores <see cref="Modifiers.ModifierDirection.Intrinsic"/> aplicados, sin boxing.</summary>
        TValue ModifiedValue { get; }
    }
}
