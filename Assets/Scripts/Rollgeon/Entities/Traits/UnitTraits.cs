namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Rasgos estáticos de una unidad, consultados por Guid desde sistemas transversales
    /// (Casillas Especiales, pathing IA). <c>default(UnitTraits)</c> es deliberadamente el
    /// perfil seguro — terrestre, no jefe, personalidad Normal — así una unidad que nadie
    /// registró se comporta como el caso base en vez de romper filtros.
    /// </summary>
    public readonly struct UnitTraits
    {
        /// <summary>Voladora: ignora casillas especiales definidas "solo terrestres" (Pinchos, Hielo, Veneno).</summary>
        public readonly bool IsFlying;

        /// <summary>Jefe: inmune a las casillas especiales de las que es owner, salvo override en la definición.</summary>
        public readonly bool IsBoss;

        /// <summary>Perfil de riesgo del pathing IA. Irrelevante para el player.</summary>
        public readonly AIPersonality Personality;

        /// <summary>Flag narrativo (solo tiene sentido con Kamikaze): ignora el filtro de supervivencia entero.</summary>
        public readonly bool KamikazeIgnoresSurvival;

        public UnitTraits(bool isFlying, bool isBoss,
            AIPersonality personality = AIPersonality.Normal,
            bool kamikazeIgnoresSurvival = false)
        {
            IsFlying = isFlying;
            IsBoss = isBoss;
            Personality = personality;
            KamikazeIgnoresSurvival = kamikazeIgnoresSurvival;
        }

        /// <summary>Perfil del player y de cualquier unidad sin data propia.</summary>
        public static UnitTraits DefaultGround => default;
    }
}
