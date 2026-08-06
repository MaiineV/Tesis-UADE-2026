namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Modo del "pasivo" anti-repetición (A/B global del jugador). Mutuamente excluyentes:
    /// <list type="bullet">
    ///   <item><description><b>Combo</b> (default): repetir el ÚLTIMO combo hace 0 daño y la
    ///   UI muestra la advertencia "Combo repetido: 0 daño".</description></item>
    ///   <item><description><b>Dice</b>: bloquea un dado al azar al inicio de cada turno del
    ///   jugador (mecánica de candado existente), independiente de la IA del boss.</description></item>
    /// </list>
    /// <para>Combo es el índice 0 → default al deserializar un enum sin autorar.</para>
    /// </summary>
    public enum AntiRepeatMode
    {
        Combo,
        Dice,
    }
}
