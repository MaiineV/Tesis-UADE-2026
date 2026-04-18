namespace Rollgeon.Combat.Random
{
    /// <summary>
    /// Abstracción minimal sobre <see cref="System.Random"/> para permitir
    /// tests deterministas del <c>DefaultInitiativeProvider</c>.
    /// </summary>
    /// <remarks>
    /// La semántica del rango es la misma que <see cref="System.Random.Next(int, int)"/>:
    /// <c>minInclusive ≤ resultado &lt; maxExclusive</c>. Los llamadores que
    /// quieran "die de 1 a 6" pasan <c>rng.Next(1, 7)</c>.
    /// </remarks>
    public interface IInitiativeRng
    {
        /// <summary>
        /// Devuelve un entero en el rango <c>[minInclusive, maxExclusive)</c>.
        /// </summary>
        int Next(int minInclusive, int maxExclusive);
    }
}
