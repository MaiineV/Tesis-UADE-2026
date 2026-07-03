using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Escala una posición de cursor en espacio de PANTALLA al espacio interno del
    /// RenderTexture de la cámara. En el pipeline pixel-art la cámara renderiza a un RT
    /// chico, así que <c>cam.pixelWidth/Height</c> difiere de <c>Screen.width/height</c> y
    /// hay que corregir antes de <c>ScreenPointToRay</c>. Extraído de
    /// <see cref="TileClickHandler"/> para compartir una única implementación testeable.
    /// </summary>
    public static class RenderTextureCursor
    {
        /// <param name="screen">Posición del cursor en píxeles de pantalla.</param>
        /// <param name="screenWidth"><c>Screen.width</c>.</param>
        /// <param name="screenHeight"><c>Screen.height</c>.</param>
        /// <param name="pixelWidth"><c>cam.pixelWidth</c> (ancho del RT/target de la cámara).</param>
        /// <param name="pixelHeight"><c>cam.pixelHeight</c>.</param>
        public static Vector2 ScreenToRt(
            Vector2 screen, float screenWidth, float screenHeight, float pixelWidth, float pixelHeight)
        {
            // Guard divide-by-zero: pantalla degenerada (0 px) — devolvemos zero en vez de NaN.
            if (screenWidth <= 0f || screenHeight <= 0f) return Vector2.zero;

            return new Vector2(
                screen.x / screenWidth * pixelWidth,
                screen.y / screenHeight * pixelHeight);
        }
    }
}
