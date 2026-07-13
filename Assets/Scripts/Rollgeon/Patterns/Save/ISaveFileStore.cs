namespace Patterns.Save
{
    /// <summary>
    /// Seam de IO del <see cref="SaveSystem"/>: la lógica de §15.3 queda intacta y sólo
    /// las llamadas a disco pasan por acá, para que los tests EditMode corran sin
    /// filesystem (precedente: <c>InMemoryMetaSaveStore</c> en Meta).
    /// </summary>
    public interface ISaveFileStore
    {
        bool Exists(string path);

        byte[] Read(string path);

        /// <summary>Escritura completa del archivo. La atomicidad es responsabilidad del impl.</summary>
        void Write(string path, byte[] bytes);

        /// <summary>Borra el archivo si existe. No lanza si no existía.</summary>
        void Delete(string path);
    }
}
