using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Proyección cilíndrica del spinner de modificadores globales (patrón
    /// <c>BreakdownFeelMath</c>/<c>ChestReelMath</c>: estático y sin estado para
    /// testear en EditMode sin tweens). El tambor se fakea con dos slots: el
    /// saliente sube comprimiéndose en Y (cara que gira hacia atrás) y el
    /// entrante sube desde abajo descomprimiéndose, con θ = t·π/2.
    /// </summary>
    public static class SpinnerDrumMath
    {
        /// <summary>ScaleY del slot saliente: 1 en reposo → 0 al salir del tambor.</summary>
        public static float OutgoingScaleY(float t01)
            => Mathf.Cos(Mathf.Clamp01(t01) * Mathf.PI * 0.5f);

        /// <summary>OffsetY del slot saliente: 0 → <paramref name="travel"/> (sube).</summary>
        public static float OutgoingOffsetY(float t01, float travel)
            => travel * Mathf.Sin(Mathf.Clamp01(t01) * Mathf.PI * 0.5f);

        /// <summary>ScaleY del slot entrante: 0 → 1. Espejo exacto del saliente.</summary>
        public static float IncomingScaleY(float t01)
            => Mathf.Sin(Mathf.Clamp01(t01) * Mathf.PI * 0.5f);

        /// <summary>OffsetY del slot entrante: -<paramref name="travel"/> → 0 (llega al centro).</summary>
        public static float IncomingOffsetY(float t01, float travel)
            => -travel * Mathf.Cos(Mathf.Clamp01(t01) * Mathf.PI * 0.5f);

        /// <summary>
        /// Decel OutCubic aplicada a t ANTES de las proyecciones: el tambor arranca
        /// rápido y "frena en el objeto".
        /// </summary>
        public static float EaseSpin(float t01)
        {
            float inv = 1f - Mathf.Clamp01(t01);
            return 1f - inv * inv * inv;
        }

        /// <summary>
        /// Recorrido vertical para que el slot salga entero de la ventana visible:
        /// media ventana + medio slot (pivots al centro).
        /// </summary>
        public static float Travel(float visibleHeight, float slotHeight)
            => visibleHeight * 0.5f + slotHeight * 0.5f;
    }
}
