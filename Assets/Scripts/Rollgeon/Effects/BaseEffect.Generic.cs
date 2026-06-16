using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Base genérica — la que la mayoría de autores especializa. TECHNICAL.md §8.3.
    /// <para>
    /// <typeparamref name="TArgs"/> es el payload typed del efecto (p.ej. <c>DamageArgs</c>,
    /// <c>HealArgs</c>, <c>ModifierApplyArgs</c>). <typeparamref name="TValue"/> es el tipo
    /// del valor principal (<c>int</c> para damage/heal amount, <c>float</c> para porcentajes,
    /// <c>bool</c> para flags).
    /// </para>
    /// <para>
    /// Los <c>ResolveArgs</c> / <c>ResolveValue</c> son virtuales y lanzan
    /// <see cref="NotImplementedException"/> por default — los concretes que usen el patrón
    /// genérico los overridean leyendo de sus propios <c>[SerializeField]</c> o via readers.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BaseEffect<TArgs, TValue> : BaseEffect
    {
        /// <summary>Resuelve el payload <typeparamref name="TArgs"/> del efecto para el contexto.</summary>
        protected virtual TArgs ResolveArgs(EffectContext context)
        {
            throw new NotImplementedException(
                $"{GetType().Name}.ResolveArgs is not implemented. Override it in the concrete effect " +
                "or change the constructor to pre-populate args.");
        }

        /// <summary>Resuelve el valor principal <typeparamref name="TValue"/> (constant, entity-read, …).</summary>
        protected virtual TValue ResolveValue(EffectContext context)
        {
            throw new NotImplementedException(
                $"{GetType().Name}.ResolveValue is not implemented. Override it if the effect declares IUsesValue.");
        }
    }
}
