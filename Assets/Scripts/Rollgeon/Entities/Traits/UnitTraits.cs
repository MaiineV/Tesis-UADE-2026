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

        /// <summary>Sin sangre: inelegible como fuente/target de efectos de Sangrado (Feature#0084, Blood Transfusion).</summary>
        public readonly bool Bloodless;

        /// <summary>No se puede desplazar por empuje/atracción/swap (Feature#0084: Grapple Claw, Probability Drive).</summary>
        public readonly bool Immovable;

        /// <summary>
        /// Inmune a Aturdido. Nota: <c>IStunService.ApplyStun</c> NO lo consulta — es el
        /// caller (ej. <c>CombatantQuery.IsStunnable</c>) quien debe gatear el intento antes
        /// de llamar al servicio.
        /// </summary>
        public readonly bool StunImmune;

        public UnitTraits(bool isFlying, bool isBoss,
            AIPersonality personality = AIPersonality.Normal,
            bool kamikazeIgnoresSurvival = false,
            bool bloodless = false,
            bool immovable = false,
            bool stunImmune = false)
        {
            IsFlying = isFlying;
            IsBoss = isBoss;
            Personality = personality;
            KamikazeIgnoresSurvival = kamikazeIgnoresSurvival;
            Bloodless = bloodless;
            Immovable = immovable;
            StunImmune = stunImmune;
        }

        /// <summary>Perfil del player y de cualquier unidad sin data propia.</summary>
        public static UnitTraits DefaultGround => default;
    }
}
