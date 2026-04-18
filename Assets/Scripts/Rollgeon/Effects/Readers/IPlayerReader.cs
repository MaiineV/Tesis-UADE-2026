namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Reader especializado en leer desde el jugador (sin necesidad de resolver la entity
    /// explícitamente — el reader consulta el <c>PlayerService</c> internamente).
    /// TECHNICAL.md §8.6. Placeholder en esta foundation — la implementación concreta
    /// vive en Foundation#0003 / Rollgeon.Player cuando mergee.
    /// </summary>
    public interface IPlayerReader<T>
    {
        /// <summary>Lee el valor desde el player contextual (singleton lógico).</summary>
        T Read();
    }
}
