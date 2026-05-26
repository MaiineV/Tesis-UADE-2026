namespace Rollgeon.Dice
{
    /// <summary>
    /// Resultado readonly de <c>IRerollBudgetService.QueryExtraRoll</c>.
    /// Mirrors TECHNICAL.md §6.5 <c>RerollAvailability</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Query puro: no tiene side effects, no cobra energia. Produce los datos que
    /// la HUD necesita para renderizar el boton de re-roll ("gratis / 1E / blocked").
    /// </para>
    /// <para>
    /// <b>BlockedReason</b> es <c>null</c> cuando <see cref="IsAvailable"/> es true;
    /// en caso contrario trae uno de los tags estables publicados como
    /// <c>public const string</c> en <see cref="RerollBudgetService"/> — la UI
    /// puede key-off de estos strings para localizacion.
    /// </para>
    /// </remarks>
    public readonly struct RerollQueryResult
    {
        /// <summary>true si el proximo reroll sera gratis (consume <c>FreeRollsRemaining</c>).</summary>
        public readonly bool IsFreeRoll;

        /// <summary>true si el proximo reroll costara 1 energia.</summary>
        public readonly bool CostsEnergy;

        /// <summary>true si hay un proximo reroll disponible (gratis o pago).</summary>
        public bool IsAvailable => IsFreeRoll || CostsEnergy;

        /// <summary>
        /// Tag estable del motivo cuando <see cref="IsAvailable"/> es false.
        /// <c>null</c> cuando hay reroll disponible. Ver constantes en
        /// <see cref="RerollBudgetService"/>.
        /// </summary>
        public readonly string BlockedReason;

        public RerollQueryResult(bool isFreeRoll, bool costsEnergy, string blockedReason)
        {
            IsFreeRoll = isFreeRoll;
            CostsEnergy = costsEnergy;
            BlockedReason = blockedReason;
        }

        /// <summary>Factory helper: reroll gratis disponible.</summary>
        public static RerollQueryResult Free() => new RerollQueryResult(true, false, null);

        /// <summary>Factory helper: reroll pago disponible (costara energia).</summary>
        public static RerollQueryResult Paid() => new RerollQueryResult(false, true, null);

        /// <summary>Factory helper: no hay reroll disponible. <paramref name="reason"/> es un tag estable.</summary>
        public static RerollQueryResult Blocked(string reason) => new RerollQueryResult(false, false, reason);
    }
}
