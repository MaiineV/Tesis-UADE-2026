using System.Collections.Generic;
using Patterns.Save;

namespace Rollgeon.Patterns.Save.Tests
{
    /// <summary>
    /// <see cref="ISaveFileStore"/> in-memory para tests (precedente:
    /// <c>InMemoryMetaSaveStore</c>). Permite presembrar bytes corruptos y contar writes.
    /// </summary>
    public sealed class InMemorySaveFileStore : ISaveFileStore
    {
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();

        public int WriteCount;

        public bool Exists(string path) => Files.ContainsKey(path);

        public byte[] Read(string path) => Files[path];

        public void Write(string path, byte[] bytes)
        {
            Files[path] = bytes;
            WriteCount++;
        }

        public void Delete(string path)
        {
            Files.Remove(path);
        }
    }
}
