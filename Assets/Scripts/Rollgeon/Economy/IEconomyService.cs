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

        /// <summary>
        /// Setea el balance a un valor absoluto y notifica <c>OnGoldChanged</c>.
        /// Para transiciones de sesión (inicio de tutorial, fresh run post-tutorial) —
        /// el service es Global y no se resetea solo entre runs.
        /// </summary>
        void ResetTo(int amount);

        /// <summary>
        /// Piso del balance: <see cref="Spend"/> nunca deja el oro por debajo de este
        /// valor. <c>0</c> sin modificadores; negativo con Tarjeta de Crédito (−30).
        /// Default members para no romper los fakes que solo implementan el contrato base.
        /// </summary>
        int MinGold => 0;

        /// <summary>
        /// Registra un piso de oro bajo <paramref name="sourceId"/> (item id). Con varios
        /// registrados gana el más bajo. <paramref name="floor"/> ≥ 0 es no-op.
        /// </summary>
        void SetGoldFloor(string sourceId, int floor) { }

        /// <summary>
        /// Quita el piso de <paramref name="sourceId"/>. No confisca oro: una deuda
        /// existente queda y se salda con el próximo <see cref="Add"/>.
        /// </summary>
        void ClearGoldFloor(string sourceId) { }
    }
}
