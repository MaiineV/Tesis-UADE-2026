namespace Rollgeon.Meta
{
    /// <summary>
    /// Backend de persistencia del save de meta-progresión (#164). La implementación
    /// real es <see cref="FileMetaSaveStore"/> (JSON en <c>persistentDataPath</c>);
    /// los tests inyectan un store in-memory vía
    /// <c>MetaProgressionService.ConfigureForTests</c>.
    /// </summary>
    public interface IMetaSaveStore
    {
        /// <summary>Carga el snapshot persistido, o <c>null</c> si no existe / está corrupto.</summary>
        MetaProgressionSnapshot Load();

        /// <summary>Persiste el snapshot de forma inmediata (write-through).</summary>
        void Save(MetaProgressionSnapshot snapshot);

        /// <summary>Borra el save persistido. No-op si no existe.</summary>
        void Delete();
    }
}
