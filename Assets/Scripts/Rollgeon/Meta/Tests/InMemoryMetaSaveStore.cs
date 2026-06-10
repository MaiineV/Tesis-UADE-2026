namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// <see cref="IMetaSaveStore"/> in-memory para tests deterministas — los tests
    /// unitarios no tocan filesystem (rules/test-standards). Cuenta los writes para
    /// verificar el write-through inmediato del unlock.
    /// </summary>
    public sealed class InMemoryMetaSaveStore : IMetaSaveStore
    {
        public MetaProgressionSnapshot Stored;
        public int SaveCount;

        public MetaProgressionSnapshot Load() => Stored;

        public void Save(MetaProgressionSnapshot snapshot)
        {
            Stored = snapshot;
            SaveCount++;
        }
    }
}
