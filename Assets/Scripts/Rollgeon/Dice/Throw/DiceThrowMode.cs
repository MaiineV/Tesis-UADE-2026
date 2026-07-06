namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Modo de tirada del jugador (CNF-008). <see cref="Classic"/> = botón Roll actual
    /// (resolución instantánea). <see cref="TwoD"/> = dados arrojables sobre el canvas
    /// (resultado precalculado, física cosmética). <see cref="ThreeD"/> = dados físicos
    /// (Rigidbody; la cara superior leída ES el resultado).
    /// </summary>
    /// <remarks>
    /// Serializado por valor int en <c>DiceThrowSettingsSO</c> — no reordenar entradas.
    /// </remarks>
    public enum DiceThrowMode
    {
        Classic = 0,
        TwoD = 1,
        ThreeD = 2,
    }
}
