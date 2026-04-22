namespace Rollgeon.Dice
{
    /// <summary>
    /// Tipos de dado del Dice Builder. TECHNICAL.md §6.1.
    /// </summary>
    public enum DiceType
    {
        D4,
        D6,
        D8,
        D10,
        D12,
        D20,
    }

    /// <summary>
    /// Extensiones de <see cref="DiceType"/> con la tabla de caras y el tope por
    /// bolsa que define el GD. TECHNICAL.md §6.1.
    /// </summary>
    public static class DiceTypeExt
    {
        /// <summary>Cara máxima del dado (1..MaxFace inclusivo).</summary>
        public static int MaxFace(this DiceType t) => t switch
        {
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
            DiceType.D4 => 5,
            DiceType.D6 => 5,
            DiceType.D8 => 4,
            DiceType.D10 => 3,
            DiceType.D12 => 2,
            DiceType.D20 => 1,
            _ => 5,
        };
    }
}
