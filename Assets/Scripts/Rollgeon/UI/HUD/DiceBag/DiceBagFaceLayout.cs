using UnityEngine;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Math del tamaño responsive de las caras del dado seleccionado: más caras,
    /// celdas más chicas (mock "new dice bag drawer"). Mientras la celda calculada
    /// no toque el piso <see cref="MinCell"/> entra todo en una fila; por debajo
    /// (d20 en banda angosta) el grid Flexible wrapea a la fila siguiente en vez de
    /// achicar más. El view escribe el resultado en el <c>cellSize</c> del grid.
    /// </summary>
    public static class DiceBagFaceLayout
    {
        /// <summary>Celda máxima — el tamaño cómodo de un d3/d6 (48 px hoy).</summary>
        public const float MaxCell = 48f;

        /// <summary>Celda mínima — piso de legibilidad del número (d20 en banda angosta).</summary>
        public const float MinCell = 22f;

        /// <summary>
        /// Lado de la celda para que <paramref name="faces"/> caras + spacing entren
        /// en <paramref name="bandWidth"/> en una fila, clampeado a [MinCell, MaxCell].
        /// Si el resultado quedó en MinCell puede no entrar todo — ahí wrapea el grid.
        /// </summary>
        public static float CellSize(int faces, float bandWidth, float spacing)
        {
            if (faces <= 0) return MaxCell;
            float fit = (bandWidth - spacing * (faces - 1)) / faces;
            return Mathf.Clamp(fit, MinCell, MaxCell);
        }
    }
}
