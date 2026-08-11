using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Lógica pura del feel del reveal gacha (patrón <c>BreakdownFeelMath</c>):
    /// intensidad por rareza, pitch del tick, rate-limit y count-up. Estático y
    /// sin estado para que el juice sea testeable en EditMode sin escena.
    /// </summary>
    public static class ChestRevealFeelMath
    {
        /// <summary>
        /// Intensidad de drama 0..1 por tier: Common apenas se siente, Legendary es
        /// un evento. Alimenta todos los knobs por-rareza vía <see cref="Knob"/>.
        /// </summary>
        public static float Intensity01(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return 0.4f;
                case ItemRarity.Rare: return 0.7f;
                case ItemRarity.Legendary: return 1f;
                default: return 0.15f;
            }
        }

        /// <summary>Lerp clampeado de un knob min..max por la intensidad de rareza.</summary>
        public static float Knob(float min, float max, float intensity01)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01(intensity01));
        }

        /// <summary>
        /// Pitch del tick por progreso del spin (no por índice): sube mientras la
        /// tira corre, y como el spin desacelera al final los ticks se espacian
        /// solos. Clampeado al rango sano de <c>PlaySfx2D</c>.
        /// </summary>
        public static float TickPitch(float progress01, float basePitch, float maxPitch)
        {
            float pitch = Mathf.Lerp(basePitch, maxPitch, Mathf.Clamp01(progress01));
            return Mathf.Clamp(pitch, 0.5f, 2f);
        }

        /// <summary>
        /// Próximo instante permitido para un SFX repetitivo (tick del reel,
        /// count-up): el intervalo se achica con la velocidad de juego para que a
        /// x4 el limiter no silencie el spin entero, sin saturar el mixer.
        /// </summary>
        public static float NextTickTime(float now, float minInterval, int speedMultiplier)
        {
            return now + Mathf.Max(0.005f, minInterval) / Mathf.Max(1, speedMultiplier);
        }

        /// <summary>El micro-hitstop del landing es solo para el tier máximo.</summary>
        public static bool HitstopAllowed(ItemRarity rarity) => rarity == ItemRarity.Legendary;

        /// <summary>El duck de música del landing/climax aplica de Rare para arriba.</summary>
        public static bool DuckAllowed(ItemRarity rarity) => rarity >= ItemRarity.Rare;

        /// <summary>
        /// Monto mostrado por el count-up de oro en t 0..1: monotónico, arranca en
        /// 0 y termina exacto en <paramref name="total"/> (sin drift de redondeo).
        /// </summary>
        public static int CountUpShown(float t01, int total)
        {
            if (total <= 0) return 0;
            if (t01 >= 1f) return total;
            if (t01 <= 0f) return 0;
            return Mathf.Min(total, Mathf.FloorToInt(t01 * total));
        }
    }
}
