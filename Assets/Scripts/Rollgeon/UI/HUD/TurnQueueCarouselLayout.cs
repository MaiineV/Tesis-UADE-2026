using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Parámetros visuales del carrusel de turnos. Serializado inline en
    /// <see cref="TurnQueueView"/> para tunear escalas/alphas desde el inspector.
    /// </summary>
    [Serializable]
    public struct TurnCarouselConfig
    {
        [Tooltip("Escala del slot activo (izquierda de todo).")]
        public float ActiveScale;

        [Tooltip("Escala de los slots que siguen (a la derecha del activo).")]
        public float UpcomingScale;

        [Tooltip("Alpha del CanvasGroup de los slots que siguen (menos vistos).")]
        [Range(0f, 1f)]
        public float UpcomingAlpha;

        [Tooltip("Ancho base del slot sin escalar (el prefab TurnSlot es 100x100).")]
        public float SlotWidth;

        [Tooltip("Espacio horizontal entre bordes de slots adyacentes.")]
        public float Spacing;

        public static TurnCarouselConfig Default => new TurnCarouselConfig
        {
            ActiveScale = 0.7f,
            UpcomingScale = 0.6f,
            UpcomingAlpha = 0.7f,
            SlotWidth = 100f,
            Spacing = 24f,
        };
    }

    /// <summary>
    /// Matemática pura del carrusel de turnos: el actor activo a la izquierda de
    /// todo (posición 0) y los próximos a su derecha (+1..+4), en loop. Sin estado
    /// ni UnityEngine.Object — testeable en EditMode.
    /// </summary>
    /// <remarks>
    /// La cola de <c>TurnOrderService</c> es circular: la ventana se resuelve con
    /// aritmética modular, por lo que con menos de 5 participantes un mismo guid
    /// aparece en más de una posición (loop de repetición estilo Expedition 33).
    /// </remarks>
    public static class TurnQueueCarouselLayout
    {
        /// <summary>Cantidad de próximos turnos visibles a la derecha del activo.</summary>
        public const int UpcomingCount = 4;

        /// <summary>Cantidad total de slots visibles (activo + próximos).</summary>
        public const int WindowSize = UpcomingCount + 1;

        /// <summary>
        /// Guid que ocupa la posición relativa <paramref name="relPos"/> (0..+4)
        /// cuando el actor activo es <c>order[cursor]</c>. Wrap circular.
        /// </summary>
        public static Guid GuidAt(IReadOnlyList<Guid> order, int cursor, int relPos)
        {
            if (order == null || order.Count == 0)
            {
                throw new InvalidOperationException(
                    "[TurnQueueCarouselLayout] GuidAt requires a non-empty order.");
            }

            int n = order.Count;
            int idx = ((cursor + relPos) % n + n) % n;
            return order[idx];
        }

        /// <summary>Escala del slot en la posición relativa dada.</summary>
        public static float GetScale(int relPos, in TurnCarouselConfig cfg)
        {
            return relPos == 0 ? cfg.ActiveScale : cfg.UpcomingScale;
        }

        /// <summary>Alpha del CanvasGroup del slot en la posición relativa dada.</summary>
        public static float GetAlpha(int relPos, in TurnCarouselConfig cfg)
        {
            return relPos == 0 ? 1f : cfg.UpcomingAlpha;
        }

        /// <summary>
        /// X del centro del slot (pivot centro) medida desde el centro del slot
        /// activo (x=0). Cada paso acumula la mitad del ancho escalado de ambos
        /// vecinos + spacing, así los bordes quedan equiespaciados aunque el
        /// activo sea más grande. Solo posiciones 0..+N.
        /// </summary>
        public static float GetX(int relPos, in TurnCarouselConfig cfg)
        {
            float x = 0f;
            for (int i = 1; i <= relPos; i++)
            {
                float prevHalf = cfg.SlotWidth * GetScale(i - 1, cfg) * 0.5f;
                float currHalf = cfg.SlotWidth * GetScale(i, cfg) * 0.5f;
                x += prevHalf + cfg.Spacing + currHalf;
            }
            return x;
        }
    }
}
