using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Lógica pura del feel del breakdown: tiers de drama, ramps, pitch y colores.
    /// Estático y sin estado para que el juice sea testeable en EditMode sin escena.
    /// </summary>
    public static class BreakdownFeelMath
    {
        /// <summary>Tier de drama por total: 0 chico (&lt; t1), 1 medio, 2 grande (≥ t2).</summary>
        public static int TierForTotal(int total, int t1, int t2)
        {
            if (total >= t2) return 2;
            return total >= t1 ? 1 : 0;
        }

        /// <summary>
        /// Factor de tiempo del dado <paramref name="index"/> (0-based): cada dado
        /// consecutivo acelera <paramref name="rampPerStep"/> hasta <paramref name="floor"/>.
        /// </summary>
        public static float SpeedRampFactor(int index, float rampPerStep, float floor)
        {
            if (index <= 0 || rampPerStep <= 0f) return 1f;
            return Mathf.Max(floor, 1f - rampPerStep * index);
        }

        /// <summary>
        /// Piso de duración de un paso que debe LEERSE (nombre de ítem del spinner,
        /// BUG-063): el ramp + game speed pueden dejarlo bajo los 0.1s y el texto no
        /// llega a verse. El skip explícito del jugador NO respeta el piso — pidió
        /// velocidad y la secuencia se comprime igual que siempre.
        /// </summary>
        public static float FloorUnlessSkipping(float seconds, float floor, bool skipping)
        {
            if (skipping) return seconds;
            return Mathf.Max(seconds, floor);
        }

        /// <summary>Pitch del dado <paramref name="index"/> (0-based), clampeado al rango sano de PlaySfx2D.</summary>
        public static float PitchForIndex(int index, float step)
        {
            return Mathf.Clamp(1f + step * Mathf.Max(0, index), 0.5f, 2f);
        }

        /// <summary>Peso 0..1 de un aporte contra el valor final de su contador (para punch proporcional).</summary>
        public static float PunchIntensity01(float amount, float finalValue)
        {
            float denom = Mathf.Max(1f, Mathf.Abs(finalValue));
            return Mathf.Clamp01(Mathf.Abs(amount) / denom);
        }

        /// <summary>Progreso 0..1 del contador hacia su final (intensidad acumulativa de bursts).</summary>
        public static float Accumulate01(float current, float final)
        {
            if (final <= 0f) return 1f;
            return Mathf.Clamp01(current / final);
        }

        /// <summary>
        /// Color "heat" de M: ≤1 neutral apagado; 1..2 interpola hacia warm;
        /// ≥2 interpola warm→hot (satura en 3).
        /// </summary>
        public static Color HeatColor(float m, Color neutral, Color warm, Color hot)
        {
            if (m <= 1f) return neutral;
            if (m < 2f) return Color.Lerp(neutral, warm, m - 1f);
            return Color.Lerp(warm, hot, Mathf.Clamp01(m - 2f));
        }

        /// <summary>El total "arde" a partir de este multiplicador.</summary>
        public static bool IsFlaming(float m, float minM) => m >= minM;

        /// <summary>Tier de combo para el pitch del chime: 2 dados = 0, 3 = 1, 4 = 2, 5+ = 3.</summary>
        public static int ComboTier(int contributingCount)
        {
            return Mathf.Clamp(contributingCount - 2, 0, 3);
        }

        /// <summary>
        /// Intensidad 0..1 de la celebración de combo jugado: mezcla mitad y mitad el
        /// tier de drama del total (<see cref="TierForTotal"/>) y el tier por cantidad
        /// de dados (<see cref="ComboTier"/>) sobre un piso — el par de 2 dados respira
        /// suave pero visible, la Generala de 5 con total alto satura en 1.
        /// </summary>
        public static float ComboCelebrateIntensity(int finalTotal, int t1, int t2, int diceCount)
        {
            float totalPart = TierForTotal(finalTotal, t1, t2) / 2f;
            float comboPart = ComboTier(diceCount) / 3f;
            return Mathf.Clamp01(0.25f + 0.375f * totalPart + 0.375f * comboPart);
        }
    }
}
