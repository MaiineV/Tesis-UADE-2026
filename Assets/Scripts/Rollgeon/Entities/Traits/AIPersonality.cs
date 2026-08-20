namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Personalidad de pathing IA (GDD Casillas Especiales, sección IA). Define cuánto
    /// riesgo acepta la unidad al pathear sobre casillas peligrosas: umbral de
    /// supervivencia (MinSurvivalHP) y multiplicador de cautela (Caution). Los valores
    /// numéricos viven en la tabla de tuning del planner, no acá — el enum solo nombra
    /// el perfil para que la data de enemigos sea legible.
    /// </summary>
    public enum AIPersonality
    {
        /// <summary>Evalúa el daño de forma estándar (MinSurvival 20%, Caution 1.0). Default de todo el catálogo.</summary>
        Normal = 0,

        /// <summary>Support / Cobarde: evita mucho el peligro (Caution 1.5).</summary>
        Support = 1,

        /// <summary>Acepta daño si gana posición (MinSurvival 10%, Caution 0.65).</summary>
        Aggressive = 2,

        /// <summary>
        /// Ignora gran parte del peligro (MinSurvival 0%, Caution 0.25). Con el flag
        /// narrativo <c>KamikazeIgnoresSurvival</c> se saltea el filtro de supervivencia entero.
        /// </summary>
        Kamikaze = 3,
    }
}
