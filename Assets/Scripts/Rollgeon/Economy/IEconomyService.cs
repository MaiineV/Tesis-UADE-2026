namespace Rollgeon.Economy
{
    /// <summary>
    /// Contrato minimal de la economía del run — oro del jugador. TECHNICAL.md
    /// §1.3 (atributo Gold) + §17.F (shop compra vía <see cref="Spend"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Emite <c>EventName.OnGoldChanged</c> con payload <c>[int current, int delta]</c>
    /// cada vez que el balance cambia. La <c>GoldCounterView</c> (§D) ya está
    /// suscripta — no necesita refresh extra.
    /// </para>
    /// <para>
    /// MVP: oro global del run, sin lifetime modifiers. Cuando aterrice el
    /// sistema de atributos real (§1.3) el service pasa a leer/escribir contra
    /// el atributo <c>Gold</c> de la <c>Entity</c> del player sin romper este
    /// contrato.
    /// </para>
    /// </remarks>
    public interface IEconomyService
    {
        /// <summary>Oro disponible del jugador.</summary>
        int CurrentGold { get; }

        /// <summary>Suma <paramref name="amount"/> al balance. <paramref name="amount"/> negativo es no-op.</summary>
        void Add(int amount);

        /// <summary>
        /// Descuenta <paramref name="amount"/> si hay fondos. Devuelve <c>true</c>
        /// si la operación se efectuó. No descuenta parcial — all-or-nothing.
        /// </summary>
        bool Spend(int amount);

        /// <summary><c>true</c> si el balance actual cubre <paramref name="amount"/>.</summary>
        bool CanAfford(int amount);
    }
}
