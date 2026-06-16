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
    /// Extensiones de <see cref="DiceType"/> con la tabla de caras, el tope por
    /// bolsa y los cupos de encantamiento (Sistema de Mejoras In-Run).
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

        /// <summary>Máximo de copias de este dado permitidas en una bolsa de 5.</summary>
        public static int MaxPerBag(this DiceType t) => t switch
        {
            DiceType.D3 => 5,
            DiceType.D4 => 5,
            DiceType.D6 => 5,
            DiceType.D8 => 4,
            DiceType.D10 => 3,
            DiceType.D12 => 2,
            DiceType.D20 => 1,
            _ => 5,
        };

        /// <summary>
        /// Cupos de encantamiento disponibles por dado. Sala de Encantamiento — GDD.
        /// Más caras = más cupos, premiando especialización en dados grandes.
        /// </summary>
        public static int MaxEnchantmentSlots(this DiceType t) => t switch
        {
            DiceType.D3 => 1,
            DiceType.D4 => 1,
            DiceType.D6 => 2,
            DiceType.D8 => 2,
            DiceType.D10 => 3,
            DiceType.D12 => 3,
            DiceType.D20 => 4,
            _ => 1,
        };
    }
}
