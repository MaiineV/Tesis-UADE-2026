using UnityEngine;

namespace Rollgeon.UI.Menu
{
    /// <summary>
    /// Matemática pura del juice de menú (hover, stagger, ciclo de color,
    /// pulso). Separada de los MonoBehaviours para poder testearla en
    /// EditMode sin escena.
    /// </summary>
    public static class MenuJuiceMath
    {
        // Fases a 2π/3: la suma de los tres senos es constante (1.5), así el
        // blend queda siempre convexo dentro del triángulo de la paleta.
        private const float PhaseB = 2f * Mathf.PI / 3f;
        private const float PhaseC = 4f * Mathf.PI / 3f;

        /// <summary>
        /// Factor de lerp frame-rate-independiente equivalente al
        /// <c>lerp(a, b, dt*k)</c> del video de referencia, pero estable a
        /// cualquier fps.
        /// </summary>
        public static float SmoothLerpFactor(float speed, float deltaTime)
        {
            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        /// <summary>
        /// Ciclo suave entre tres colores de la paleta. Combinación convexa:
        /// nunca produce un color fuera del rango de los tres de entrada.
        /// </summary>
        public static Color PastelCycle(float time, float speed, Color a, Color b, Color c)
        {
            float t = time * speed;
            float wa = 0.5f + 0.5f * Mathf.Sin(t);
            float wb = 0.5f + 0.5f * Mathf.Sin(t + PhaseB);
            float wc = 0.5f + 0.5f * Mathf.Sin(t + PhaseC);
            float sum = wa + wb + wc;
            return (a * wa + b * wb + c * wc) / sum;
        }

        public static float StaggerDelay(int index, float step)
        {
            return index * step;
        }

        public static float PulseScale(float time, float frequency, float amplitude)
        {
            return 1f + Mathf.Sin(time * frequency) * amplitude;
        }

        public static float UnderlineTargetWidth(float textWidth, float pad)
        {
            return textWidth + pad;
        }
    }
}
