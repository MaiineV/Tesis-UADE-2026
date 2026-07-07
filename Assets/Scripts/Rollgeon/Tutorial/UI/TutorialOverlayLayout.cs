using UnityEngine;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>
    /// Matemática pura del layout del overlay (testeable en EditMode, sin Unity UI):
    /// colocación del popup en el cuadrante opuesto al anchor con histéresis, y
    /// posición/rotación de la flecha sobre el borde del recorte.
    /// </summary>
    public static class TutorialOverlayLayout
    {
        /// <summary>
        /// Centro del popup para un anchor dado: el punto característico del
        /// cuadrante OPUESTO (28% / 72% de cada eje), clampeado por margen.
        /// </summary>
        public static Vector2 ResolvePopupCenter(
            Vector2 anchorScreenPos, Vector2 screenSize, float margin)
        {
            float x = anchorScreenPos.x < screenSize.x * 0.5f ? screenSize.x * 0.72f : screenSize.x * 0.28f;
            float y = anchorScreenPos.y < screenSize.y * 0.5f ? screenSize.y * 0.72f : screenSize.y * 0.28f;
            return new Vector2(
                Mathf.Clamp(x, margin, screenSize.x - margin),
                Mathf.Clamp(y, margin, screenSize.y - margin));
        }

        /// <summary>
        /// Cuadrante con histéresis: mantiene el cuadrante previo salvo que el anchor
        /// cruce la línea media por más de <paramref name="hysteresisFraction"/> de
        /// pantalla — evita el flip-flop del popup con anchors en movimiento.
        /// Cuadrantes: bit 0 = anchor en mitad derecha, bit 1 = anchor en mitad superior.
        /// </summary>
        public static int ResolveQuadrantWithHysteresis(
            Vector2 anchorScreenPos, Vector2 screenSize, int previousQuadrant, float hysteresisFraction)
        {
            float bandX = screenSize.x * hysteresisFraction;
            float bandY = screenSize.y * hysteresisFraction;
            float midX = screenSize.x * 0.5f;
            float midY = screenSize.y * 0.5f;

            bool prevRight = (previousQuadrant & 1) != 0;
            bool prevTop = (previousQuadrant & 2) != 0;

            bool right = prevRight
                ? anchorScreenPos.x > midX - bandX
                : anchorScreenPos.x > midX + bandX;
            bool top = prevTop
                ? anchorScreenPos.y > midY - bandY
                : anchorScreenPos.y > midY + bandY;

            return (right ? 1 : 0) | (top ? 2 : 0);
        }

        /// <summary>Centro del popup para un cuadrante ya resuelto (opuesto al anchor).</summary>
        public static Vector2 PopupCenterForQuadrant(int anchorQuadrant, Vector2 screenSize, float margin)
        {
            bool anchorRight = (anchorQuadrant & 1) != 0;
            bool anchorTop = (anchorQuadrant & 2) != 0;
            float x = anchorRight ? screenSize.x * 0.28f : screenSize.x * 0.72f;
            float y = anchorTop ? screenSize.y * 0.28f : screenSize.y * 0.72f;
            return new Vector2(
                Mathf.Clamp(x, margin, screenSize.x - margin),
                Mathf.Clamp(y, margin, screenSize.y - margin));
        }

        /// <summary>
        /// Posición de la flecha: sobre la línea popup→recorte, a
        /// <paramref name="gap"/> px del borde del círculo, del lado del popup.
        /// </summary>
        public static Vector2 ResolveArrowPosition(
            Vector2 cutoutCenter, float cutoutRadius, Vector2 popupCenter, float gap)
        {
            var toPopup = popupCenter - cutoutCenter;
            var dir = toPopup.sqrMagnitude > 0.0001f ? toPopup.normalized : Vector2.up;
            return cutoutCenter + dir * (cutoutRadius + gap);
        }

        /// <summary>
        /// Rotación Z (grados) para un sprite que apunta a la DERECHA en 0°,
        /// de modo que apunte desde <paramref name="arrowPos"/> hacia el recorte.
        /// </summary>
        public static float ResolveArrowRotationZ(Vector2 arrowPos, Vector2 cutoutCenter)
        {
            var dir = cutoutCenter - arrowPos;
            if (dir.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }
}
