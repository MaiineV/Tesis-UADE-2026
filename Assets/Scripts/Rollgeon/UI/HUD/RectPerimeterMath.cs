using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Parametrización del borde de un rect por longitud de arco — la usa
    /// <see cref="EndTurnEnergyHighlight"/> para repartir dots con espaciado uniforme
    /// alrededor del contorno del botón. Pura y sin estado: testeable en EditMode.
    /// </summary>
    public static class RectPerimeterMath
    {
        /// <summary>Perímetro del rect. Componentes negativas se clampan a 0.</summary>
        public static float Perimeter(Vector2 size)
        {
            float w = Mathf.Max(0f, size.x);
            float h = Mathf.Max(0f, size.y);
            return 2f * (w + h);
        }

        /// <summary>
        /// Punto del borde en coordenadas locales (rect centrado, pivot 0.5/0.5).
        /// <paramref name="t"/> = 0..1 recorre el contorno completo en sentido horario
        /// arrancando en la esquina superior-izquierda; fuera de rango wrapea
        /// (t - floor(t)). Con perímetro 0 devuelve el centro.
        /// </summary>
        public static Vector2 PointOnPerimeter(Vector2 size, float t)
        {
            float w = Mathf.Max(0f, size.x);
            float h = Mathf.Max(0f, size.y);
            float perimeter = 2f * (w + h);
            if (perimeter <= 0f) return Vector2.zero;

            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            t -= Mathf.Floor(t);
            float d = t * perimeter;

            // Cuatro segmentos encadenados por longitud de arco: top (izq→der),
            // right (arriba→abajo), bottom (der→izq), left (abajo→arriba).
            if (d < w) return new Vector2(-halfW + d, halfH);
            d -= w;
            if (d < h) return new Vector2(halfW, halfH - d);
            d -= h;
            if (d < w) return new Vector2(halfW - d, -halfH);
            d -= w;
            return new Vector2(-halfW, -halfH + d);
        }
    }
}
