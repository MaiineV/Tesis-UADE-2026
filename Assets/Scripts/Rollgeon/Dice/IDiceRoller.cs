namespace Rollgeon.Dice
{
    /// <summary>
    /// Kernel de tirada de dados. TECHNICAL.md §6.3.
    /// </summary>
    /// <remarks>
    /// La implementación es pura C# (sin <c>MonoBehaviour</c>) y se registra en el
    /// <c>ServiceLocator</c> bajo <see cref="IDiceRoller"/>. El RNG vive en la
    /// instancia, así que dos consumidores que comparten la misma instancia ven la
    /// misma secuencia — intencional para reproducibilidad por run.
    /// </remarks>
    public interface IDiceRoller
    {
        /// <summary>Tira los 5 dados de <paramref name="bag"/>. Devuelve un array
        /// del mismo largo donde cada índice es la cara obtenida (1..MaxFace).</summary>
        int[] RollAll(DiceBagSO bag);

        /// <summary>
        /// Re-tira los dados de <paramref name="bag"/> respetando el array
        /// <paramref name="keep"/>: si <c>keep[i] == true</c>, conserva
        /// <c>previousResult[i]</c>; si no, tira el dado y devuelve la cara nueva.
        /// </summary>
        /// <param name="bag">Bolsa del jugador. Debe coincidir en tamaño con
        /// <paramref name="previousResult"/>.</param>
        /// <param name="previousResult">Resultado anterior (para preservar holds).
        /// Puede ser <c>null</c>: en ese caso se rerollea todo.</param>
        /// <param name="keep">Máscara de holds. Puede ser <c>null</c> o de largo
        /// distinto al bag — los índices no cubiertos se tratan como <c>false</c>.</param>
        int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep);
    }
}
