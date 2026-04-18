using Rollgeon.Effects.Stubs;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Reader que resuelve un valor tipado <typeparamref name="T"/> desde una entidad
    /// fuente. TECHNICAL.md §8.6. Declarado acá como placeholder para desacoplar los
    /// efectos de la API concreta de <c>AttributesManager</c> (Foundation#0003).
    /// <para>
    /// Plan §10.8 — si la foundation de Attributes decide colocar la versión canónica
    /// en <c>Rollgeon.Attributes.Readers</c>, este alias local se borra en el merge
    /// post-integración. Hasta entonces cumple como contrato mínimo.
    /// </para>
    /// </summary>
    public interface IEntityReader<T>
    {
        /// <summary>Lee el valor desde la entidad dada. Implementaciones pueden cachear.</summary>
        T Read(Entity source);
    }
}
