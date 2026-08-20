namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Claves de la tabla UI para los textos del tooltip de estados. Constantes y no
    /// literales sueltos: el seeder y el runtime tienen que nombrar exactamente lo mismo,
    /// y un typo en un literal se descubre recién en pantalla.
    /// </summary>
    public static class StatusTextKeys
    {
        /// <summary>Pasiva cumpliendo sus condiciones.</summary>
        public const string Active = "status.active";

        /// <summary>Pasiva latente.</summary>
        public const string Inactive = "status.inactive";

        /// <summary>Duración restante. Toma <c>{0}</c> = turnos.</summary>
        public const string Duration = "status.duration";

        /// <summary>Último turno del efecto — evita el "1 turnos".</summary>
        public const string DurationLastTurn = "status.duration.last";

        /// <summary>Badge bajo el ícono con 1 turno restante ("1 Turno").</summary>
        public const string BadgeOneTurn = "status.badge.turn";

        /// <summary>Badge bajo el ícono con varios turnos. Toma <c>{0}</c> = turnos.</summary>
        public const string BadgeTurns = "status.badge.turns";
    }
}
