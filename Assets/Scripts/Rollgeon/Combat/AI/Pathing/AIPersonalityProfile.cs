using Rollgeon.Entities.Traits;

namespace Rollgeon.Combat.AI.Pathing
{
    /// <summary>
    /// Perfil de riesgo resuelto de una unidad: umbral de supervivencia y cautela, listos
    /// para el planner. Se construye desde <see cref="AIPersonality"/> + el flag narrativo
    /// kamikaze + la tabla de tuning (o los defaults del GDD sin tabla).
    /// </summary>
    public readonly struct AIPersonalityProfile
    {
        /// <summary>MinSurvivalHP como fracción del HP máximo.</summary>
        public readonly float MinSurvivalHpPct;

        public readonly float Caution;

        /// <summary>Solo Kamikaze con flag narrativo: ignora el filtro de supervivencia entero.</summary>
        public readonly bool SkipSurvivalFilter;

        /// <summary>Kamikaze (con o sin flag): único perfil que cruza Telegraphs letales.</summary>
        public readonly bool IsKamikaze;

        public AIPersonalityProfile(float minSurvivalHpPct, float caution,
            bool skipSurvivalFilter = false, bool isKamikaze = false)
        {
            MinSurvivalHpPct = minSurvivalHpPct;
            Caution = caution;
            SkipSurvivalFilter = skipSurvivalFilter;
            IsKamikaze = isKamikaze;
        }

        /// <summary>Normal: 20% de HP como umbral de supervivencia, cautela 1.0.</summary>
        public static AIPersonalityProfile Default => new AIPersonalityProfile(0.20f, 1.0f);

        public static AIPersonalityProfile Resolve(AIPersonality personality,
            bool kamikazeIgnoresSurvival, AIPathTuningSO tuning)
        {
            switch (personality)
            {
                case AIPersonality.Support:
                    return new AIPersonalityProfile(
                        tuning != null ? tuning.SupportMinSurvivalPct : 0.20f,
                        tuning != null ? tuning.SupportCaution : 1.5f);

                case AIPersonality.Aggressive:
                    return new AIPersonalityProfile(
                        tuning != null ? tuning.AggressiveMinSurvivalPct : 0.10f,
                        tuning != null ? tuning.AggressiveCaution : 0.65f);

                case AIPersonality.Kamikaze:
                    return new AIPersonalityProfile(
                        tuning != null ? tuning.KamikazeMinSurvivalPct : 0f,
                        tuning != null ? tuning.KamikazeCaution : 0.25f,
                        skipSurvivalFilter: kamikazeIgnoresSurvival,
                        isKamikaze: true);

                default:
                    return new AIPersonalityProfile(
                        tuning != null ? tuning.NormalMinSurvivalPct : 0.20f,
                        tuning != null ? tuning.NormalCaution : 1.0f);
            }
        }
    }
}
