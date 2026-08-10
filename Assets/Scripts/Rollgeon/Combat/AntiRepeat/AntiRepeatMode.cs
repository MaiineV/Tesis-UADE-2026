namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Modo del "pasivo" anti-repetición (A/B global del jugador). Mutuamente excluyentes:
    /// <list type="bullet">
    ///   <item><description><b>Combo</b>: repetir el ÚLTIMO combo hace 0 daño y la
    ///   UI muestra la advertencia "Combo repetido: 0 daño".</description></item>
    ///   <item><description><b>Dice</b>: bloquea un dado al azar al inicio de cada turno del
    ///   jugador, en TODO combate — independiente de la IA del boss.</description></item>
    ///   <item><description><b>None</b>: sin pasivo global. La presión anti-repetición la
    ///   aporta solo el árbol del boss (un <c>AINode_RotateBlock</c> acotado a su pelea), así
    ///   que el candado no leakea a otras salas ni se anula daño por combo repetido. Es el
    ///   modo shippeado por defecto en el config.</description></item>
    /// </list>
    /// <para>Combo es el índice 0 → default del enum al deserializar sin autorar. <b>None va al
    /// final</b> para no shiftear los ints ya serializados en configs/assets existentes.</para>
    /// </summary>
    public enum AntiRepeatMode
    {
        Combo,
        Dice,
        None,
    }
}
