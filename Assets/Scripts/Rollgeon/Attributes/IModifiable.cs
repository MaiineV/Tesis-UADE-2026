using System;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// <see cref="IAttribute"/> con stack de modificadores. Los stats de runtime
    /// implementan esta interfaz; los templates estaticos (config) basta con
    /// <see cref="IAttribute"/>. Especificado en TECHNICAL.md §2.1.
    /// </summary>
    public interface IModifiable : IAttribute
    {
        /// <summary>
        /// Valor crudo con la pipeline de modificadores intrinsecos aplicada
        /// (los que <see cref="Modifier{T}.Direction"/> == <see cref="ModifierDirection.Intrinsic"/>).
        /// Los modificadores direccionales (<see cref="ModifierDirection.Outgoing"/>
        /// / <see cref="ModifierDirection.Incoming"/>) los consume la pipeline de
        /// daño/heal (TECHNICAL.md §12, §17.M), no este accessor.
        /// </summary>
        T GetModifiedValue<T>();

        /// <summary>
        /// Hook reservado para permitir que los modificadores se re-suscriban a
        /// callbacks on-stack-change (ej: recalcular derivados). Implementacion
        /// mayormente vacia en Foundation#0003 — queda como extension point.
        /// </summary>
        void SubscribeModifier();

        /// <summary>
        /// Agrega un modificador al stack. Devuelve <c>true</c> si el tipo
        /// generico <typeparamref name="T"/> coincide con el valor del atributo.
        /// </summary>
        bool AddModifier<T>(IModifier<T> modifier);

        /// <summary>Remueve el modificador cuyo <see cref="IModifier.ModifierId"/> coincida.</summary>
        void RemoveModifier(Guid modifierId);

        /// <summary>
        /// Registra un callback que se dispara con el <see cref="IModifier.ModifierId"/>
        /// cada vez que el valor modificado cambia (por add/remove de modifiers).
        /// </summary>
        void LinkAttribute(Action<Guid> callback);
    }
}
