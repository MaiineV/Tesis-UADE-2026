namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Modificador tipado. <see cref="ApplyModifier"/> aplica la operacion
    /// declarada (via <see cref="OperationResolver"/>) sobre el valor previo.
    /// </summary>
    /// <typeparam name="T">Tipo del amount (<c>int</c> / <c>float</c> / <c>bool</c>).</typeparam>
    public interface IModifier<T> : IModifier
    {
        /// <summary>Monto a aplicar (ej: +5 HP, -0.2 multiplicador, true flag).</summary>
        T Amount { get; }

        /// <summary>Aplica el modificador a <paramref name="value"/> y devuelve el resultado.</summary>
        T ApplyModifier(T value);
    }
}
