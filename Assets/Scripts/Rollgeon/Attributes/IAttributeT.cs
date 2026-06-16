namespace Rollgeon.Attributes
{
    /// <summary>
    /// Conveniencia tipada de <see cref="IAttribute"/>. Los stats concretos
    /// (Health : IModifiable&lt;int&gt;, Speed : IModifiable&lt;int&gt;, ...) la heredan
    /// via <see cref="IModifiable{TValue}"/> para exponer un accessor sin boxing.
    /// </summary>
    /// <typeparam name="TValue">Tipo primitivo del valor (<c>int</c>, <c>float</c>, <c>bool</c>, ...).</typeparam>
    public interface IAttribute<TValue> : IAttribute
    {
        /// <summary>Accessor tipado directo. Evita el boxing que introduciria pasar por <see cref="IAttribute.GetValue{T}"/>.</summary>
        TValue Value { get; set; }
    }
}
