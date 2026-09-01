using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public abstract class EffectIntReader
    {
        public abstract int Read(EffectContext context);

        /// <summary>
        /// Variante float para consumidores que preservan fracciones (el base damage
        /// override de la fórmula N×M — Furia Contenida acumula 0.25/ronda). Default:
        /// el <see cref="Read"/> entero, así los ~15 readers existentes no cambian.
        /// Virtual sin estado serializado a propósito: NO rompe los assets Odin que
        /// serializan readers por type name contra campos <c>EffectIntReader</c>.
        /// </summary>
        public virtual float ReadFloat(EffectContext context) => Read(context);
    }
}
