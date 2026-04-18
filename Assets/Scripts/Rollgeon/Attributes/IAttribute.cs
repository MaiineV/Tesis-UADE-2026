using System;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Contrato base de un atributo: valor serializable con nombre y tipo.
    /// Representa datos estaticos sin estado de runtime (sin modificadores).
    /// Especificado en TECHNICAL.md §2.1.
    /// </summary>
    public interface IAttribute
    {
        /// <summary>
        /// Devuelve el valor crudo del atributo tipado como <typeparamref name="T"/>.
        /// Lanza <see cref="InvalidCastException"/> si <typeparamref name="T"/> no
        /// coincide con <see cref="GetValueType"/> para evitar boxing silencioso.
        /// </summary>
        T GetValue<T>();

        /// <summary>
        /// Setea el valor crudo. Lanza <see cref="InvalidCastException"/> si
        /// <typeparamref name="T"/> no coincide con <see cref="GetValueType"/>.
        /// </summary>
        void SetValue<T>(T value);

        /// <summary>Tipo runtime del valor almacenado (<c>typeof(int)</c>, <c>typeof(float)</c>, ...).</summary>
        Type GetValueType();

        /// <summary>Nombre human-readable del atributo, util para logs y debug UI.</summary>
        string GetAttributeName();

        /// <summary>
        /// Devuelve una copia independiente del atributo. Usado por
        /// <see cref="ModifiableAttributes.DuplicateAttributes"/> para inicializar
        /// una run sin mutar el asset-source (ver TECHNICAL.md §2.2).
        /// </summary>
        IAttribute Duplicate();
    }
}
