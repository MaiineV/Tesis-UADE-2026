using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Math puro del ítem flotante sobre el pedestal: fase por instancia y
    /// desplazamiento vertical. Sin estado, para poder testear el tuneo en
    /// EditMode.
    /// </summary>
    public static class PedestalItemFloatMath
    {
        private const float Tau = Mathf.PI * 2f;

        /// <summary>
        /// Fase estable derivada del XZ world del pivot, en <c>[0, 2π)</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Misma fórmula que el hash del shader <c>Rollgeon/PaletteCelItemFloat</c>,
        /// a propósito: dos ítems en la misma sala tienen que flotar desfasados sin
        /// necesitar datos por-instancia.
        /// </para>
        /// <para>
        /// La parte fraccionaria se saca en <c>double</c> y no en <c>float</c>: el
        /// producto ronda 43758, donde el ULP de un float es ~0.004, y
        /// <c>hash - Mathf.Floor(hash)</c> podía devolver un valor apenas negativo
        /// cuando el JIT evaluaba la resta con más precisión que la que había usado
        /// el <c>Floor</c>. Salía una fase negativa chiquita — inofensiva para el
        /// seno, pero es el tipo de detalle que después aparece como un bug raro en
        /// otro consumidor.
        /// </para>
        /// </remarks>
        public static float PhaseFromWorldXZ(float x, float z)
        {
            double raw = System.Math.Sin(x * 12.9898 + z * 78.233) * 43758.5453;
            double frac = raw - System.Math.Floor(raw);
            return (float)(frac * Tau);
        }

        /// <summary>Desplazamiento vertical del bob respecto de la posición de reposo.</summary>
        public static float VerticalOffset(float time, float phase, float speed, float amplitude)
        {
            return Mathf.Sin(time * speed + phase) * amplitude;
        }
    }
}
