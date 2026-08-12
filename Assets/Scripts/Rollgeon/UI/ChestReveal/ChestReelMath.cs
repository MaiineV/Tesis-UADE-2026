using UnityEngine;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Movimiento del reel (puro, testeable): posiciones de celda, offset objetivo
    /// con jitter de aterrizaje y curva de desaceleración default. La vista coloca
    /// las celdas EXCLUSIVAMENTE con esta matemática (sin LayoutGroup) para que
    /// tests y render no puedan driftear.
    /// </summary>
    public static class ChestReelMath
    {
        /// <summary>Centro X de la celda <paramref name="index"/>, medido desde el origen de la tira.</summary>
        public static float CellCenterX(int index, float cellWidth, float spacing) =>
            index * (cellWidth + spacing) + cellWidth * 0.5f;

        /// <summary>
        /// Offset de tira que deja al puntero sobre la celda ganadora, con un jitter
        /// visual (±<paramref name="maxJitter01"/> del semiancho de la celda) que
        /// nunca saca al puntero de esa celda.
        /// </summary>
        public static float TargetOffset(
            int winnerIndex, float cellWidth, float spacing, float jitter01, float maxJitter01)
        {
            float clampedJitter = Mathf.Clamp(jitter01, -1f, 1f) * Mathf.Clamp01(maxJitter01);
            return CellCenterX(winnerIndex, cellWidth, spacing) + clampedJitter * (cellWidth * 0.5f);
        }

        /// <summary>
        /// Celda bajo el puntero para un offset dado — alimenta el tick de audio.
        /// Monótona no-decreciente respecto del offset.
        /// </summary>
        public static int CellIndexAtOffset(float offset, float cellWidth, float spacing, int totalCells)
        {
            float pitch = cellWidth + spacing;
            if (pitch <= 0f || totalCells <= 0) return 0;
            int index = Mathf.FloorToInt(offset / pitch);
            return Mathf.Clamp(index, 0, totalCells - 1);
        }

        /// <summary>
        /// Índice del ganador dentro de la tira: cerca del final (deja celdas de
        /// "overshoot" visual) y nunca antes de <paramref name="minSpinCells"/>.
        /// </summary>
        public static int PickWinnerIndex(int totalCells, int minSpinCells, System.Random rng)
        {
            if (totalCells <= 1) return 0;
            int lower = Mathf.Clamp(minSpinCells, 0, totalCells - 1);
            int upper = Mathf.Max(lower, totalCells - 4);
            return upper > lower ? rng.Next(lower, upper + 1) : lower;
        }

        /// <summary>Curva default cuando no hay AnimationCurve autorada: OutQuart.</summary>
        public static float Decelerate01(float t01)
        {
            float t = Mathf.Clamp01(t01);
            float inv = 1f - t;
            return 1f - inv * inv * inv * inv;
        }
    }
}
