namespace Rollgeon.Upgrades
{
    /// <summary>
    /// Canales del Sistema de Mejoras In-Run. Cada canal tiene su propio punto
    /// de obtención y su propia lógica de aplicación.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Dice</b> — Encantamientos sobre dados (Sala de Encantamiento, aleatorio).</description></item>
    /// <item><description><b>Combo</b> — Pasivas que modifican combos del Contrato (tienda, elegido).</description></item>
    /// <item><description><b>Character</b> — Mejoras a stats base del personaje (reward de jefe/sala desafío, elegido).</description></item>
    /// </list>
    /// </remarks>
    public enum UpgradeChannel
    {
        Dice,
        Combo,
        Character,
    }
}
