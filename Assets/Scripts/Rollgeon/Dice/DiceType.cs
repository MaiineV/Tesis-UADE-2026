namespace Rollgeon.Dice
{
    /// <summary>
    /// Tipos de dado del Dice Builder. TECHNICAL.md §6.1.
    /// </summary>
    /// <remarks>
    /// <b>Stability convention.</b> Unity serializa enums por valor int. Cualquier
    /// nueva entrada se agrega <i>al final</i> para no shiftear los valores de las
    /// entradas existentes (rompería assets ya autorados — DiceBagSO, ClassHeroSO,
    /// etc.).
    /// </remarks>
    public enum DiceType
    {
        D4,
        D6,
        D8,
        D10,
        D12,
        D20,
        // Encantamientos pack — agregado por el Sistema de Mejoras In-Run.
        // El GDD dice "D3: 1 cupo", así que se agrega aquí (al final) sin tocar
        // los valores existentes.
        D3,
    }

    /// <summary>
    /// Extensiones de <see cref="DiceType"/> con la tabla de caras y los cupos
    /// de encantamiento (Sistema de Mejoras In-Run).
    /// </summary>
    public static class DiceTypeExt
    {
        /// <summary>Cara máxima del dado (1..MaxFace inclusivo).</summary>
        public static int MaxFace(this DiceType t) => t switch
        {
            DiceType.D3 => 3,
            DiceType.D4 => 4,
            DiceType.D6 => 6,
            DiceType.D8 => 8,
            DiceType.D10 => 10,
            DiceType.D12 => 12,
            DiceType.D20 => 20,
            _ => 6,
        };

        /// <summary>
        /// Valor esperado (EV) de una tirada de este dado: <c>(MaxFace + 1) / 2</c>.
        /// Usado por la fórmula de daño v2 (Spec Daño §multi_dmg_combo) para ponderar
        /// la calidad de los dados que formaron el combo ganador.
        /// </summary>
        public static float ExpectedValue(this DiceType t) => t switch
        {
            DiceType.D3 => 2.0f,
            DiceType.D4 => 2.5f,
            DiceType.D6 => 3.5f,
            DiceType.D8 => 4.5f,
            DiceType.D10 => 5.5f,
            DiceType.D12 => 6.5f,
            DiceType.D20 => 10.5f,
            _ => 3.5f,
        };

        /// <summary>
        /// Línea base del multiplicador de daño de combo (EV del d6, ×1.00). Divisor
        /// fijo del spec de daño v2 — no depender del build del jugador.
        /// </summary>
        public const float BaselineExpectedValue = 3.5f;
    }
}
